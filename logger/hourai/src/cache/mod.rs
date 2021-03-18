pub mod model;

mod builder;
mod config;
mod updates;

pub use self::{
    builder::InMemoryCacheBuilder,
    config::{Config, ResourceType},
    updates::UpdateCache,
};

use self::model::*;
use dashmap::{mapref::entry::Entry, DashMap, DashSet};
use std::{
    collections::HashSet,
    hash::Hash,
    sync::{Arc, Mutex},
};
use twilight_model::{
    channel::{Group, GuildChannel, PrivateChannel},
    gateway::presence::{Presence, Status, UserOrId},
    guild::{Guild, Member, Permissions, Role},
    id::{ChannelId, GuildId, RoleId, UserId},
    user::CurrentUser,
    voice::VoiceState,
};

#[derive(Debug)]
struct GuildItem<T> {
    data: Arc<T>,
    guild_id: GuildId,
}

fn upsert_guild_item<K: Eq + Hash, V: PartialEq>(
    map: &DashMap<K, GuildItem<V>>,
    guild_id: GuildId,
    k: K,
    v: V,
) -> Arc<V> {
    match map.entry(k) {
        Entry::Occupied(e) if *e.get().data == v => Arc::clone(&e.get().data),
        Entry::Occupied(mut e) => {
            let v = Arc::new(v);
            e.insert(GuildItem {
                data: Arc::clone(&v),
                guild_id,
            });

            v
        }
        Entry::Vacant(e) => Arc::clone(
            &e.insert(GuildItem {
                data: Arc::new(v),
                guild_id,
            })
            .data,
        ),
    }
}

fn upsert_item<K: Eq + Hash, V: PartialEq>(map: &DashMap<K, Arc<V>>, k: K, v: V) -> Arc<V> {
    match map.entry(k) {
        Entry::Occupied(e) if **e.get() == v => Arc::clone(e.get()),
        Entry::Occupied(mut e) => {
            let v = Arc::new(v);
            e.insert(Arc::clone(&v));

            v
        }
        Entry::Vacant(e) => {
            let v = Arc::new(v);
            e.insert(Arc::clone(&v));

            v
        }
    }
}

// When adding a field here, be sure to add it to `InMemoryCache::clear` if
// necessary.
#[derive(Debug, Default)]
struct InMemoryCacheRef {
    config: Arc<Config>,
    channels_guild: DashMap<ChannelId, GuildItem<CachedChannel>>,
    channels_private: DashMap<ChannelId, Arc<PrivateChannel>>,
    // So long as the lock isn't held across await or panic points this is fine.
    current_user: Mutex<Option<Arc<CurrentUser>>>,
    groups: DashMap<ChannelId, Arc<Group>>,
    guilds: DashMap<GuildId, Arc<CachedGuild>>,
    guild_channels: DashMap<GuildId, HashSet<ChannelId>>,
    guild_presences: DashMap<GuildId, HashSet<UserId>>,
    guild_roles: DashMap<GuildId, HashSet<RoleId>>,
    roles: DashMap<RoleId, GuildItem<Role>>,
    unavailable_guilds: DashSet<GuildId>,
    voice_states: DashMap<(GuildId, UserId), ChannelId>,
    pending_members: DashSet<(GuildId, UserId)>,
}

/// A thread-safe, in-memory-process cache of Discord data. It can be cloned and
/// sent to other threads.
///
/// This is an implementation of a cache designed to be used by only the
/// current process.
///
/// Events will only be processed if they are properly expressed with
/// [`Intents`]; refer to function-level documentation for more details.
///
/// # Cloning
///
/// The cache internally wraps its data within an Arc. This means that the cache
/// can be cloned and passed around tasks and threads cheaply.
///
/// # Design and Performance
///
/// The defining characteristic of this cache is that returned types (such as a
/// guild or user) do not use locking for access. The internals of the cache use
/// a concurrent map for mutability and the returned types themselves are Arcs.
/// If a user is retrieved from the cache, an `Arc<User>` is returned. If a
/// reference to that user is held but the cache updates the user, the reference
/// held by you will be outdated, but still exist.
///
/// The intended use is that data is held outside the cache for only as long
/// as necessary, where the state of the value at that point time doesn't need
/// to be up-to-date. If you need to ensure you always have the most up-to-date
/// "version" of a cached resource, then you can re-retrieve it whenever you use
/// it: retrieval operations are extremely cheap.
///
/// For example, say you're deleting some of the guilds of a channel. You'll
/// probably need the guild to do that, so you retrieve it from the cache. You
/// can then use the guild to update all of the channels, because for most use
/// cases you don't need the guild to be up-to-date in real time, you only need
/// its state at that *point in time* or maybe across the lifetime of an
/// operation. If you need the guild to always be up-to-date between operations,
/// then the intent is that you keep getting it from the cache.
///
/// [`Intents`]: ::twilight_model::gateway::Intents
#[derive(Clone, Debug, Default)]
pub struct InMemoryCache(Arc<InMemoryCacheRef>);

/// Implemented methods and types for the cache.
impl InMemoryCache {
    pub fn new() -> Self {
        Self::default()
    }

    fn new_with_config(config: Config) -> Self {
        Self(Arc::new(InMemoryCacheRef {
            config: Arc::new(config),
            ..Default::default()
        }))
    }

    /// Create a new builder to configure and construct an in-memory cache.
    pub fn builder() -> InMemoryCacheBuilder {
        InMemoryCacheBuilder::new()
    }

    /// Returns a copy of the config cache.
    pub fn config(&self) -> Config {
        (*self.0.config).clone()
    }

    /// Update the cache with an event from the gateway.
    pub fn update(&self, value: &impl UpdateCache) {
        value.update(self);
    }

    /// Finds which voice channel a user is in for a given Guild.
    /// This runs O(1) time.
    pub fn voice_state(&self, guild_id: GuildId, user_id: UserId) -> Option<ChannelId> {
        self.0
            .voice_states
            .get(&(guild_id, user_id))
            .map(|kv| *kv.value())
    }

    /// Finds all of the users in a given voice channel.
    /// This runs O(n) time if n is the number of the number of user voice states cached.
    ///
    /// This linear time scaling is generally fine since the number of users in voice channels is
    /// signifgantly lower than the sum total of all users visible to the bot.
    pub fn voice_channel_users(&self, channel_id: ChannelId) -> Vec<UserId> {
        self.0
            .voice_states
            .iter()
            .filter(|kv| *kv.value() == channel_id)
            .map(|kv| kv.key().1)
            .collect()
    }

    /// Gets a channel by ID.
    ///
    /// This is an O(1) operation. This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn guild_channel(&self, channel_id: ChannelId) -> Option<Arc<CachedChannel>> {
        self.0
            .channels_guild
            .get(&channel_id)
            .map(|x| Arc::clone(&x.data))
    }

    /// Gets the current user.
    ///
    /// This is an O(1) operation.
    pub fn current_user(&self) -> Option<Arc<CurrentUser>> {
        self.0
            .current_user
            .lock()
            .expect("current user poisoned")
            .clone()
    }

    /// Gets a group by ID.
    ///
    /// This is an O(1) operation.
    pub fn group(&self, channel_id: ChannelId) -> Option<Arc<Group>> {
        self.0
            .groups
            .get(&channel_id)
            .map(|r| Arc::clone(r.value()))
    }

    /// Gets all of the IDs of the guilds in the cache.
    ///
    /// This is an O(n) operation. This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn guilds(&self) -> Vec<GuildId> {
        self.0.guilds.iter().map(|r| *r.key()).collect()
    }

    /// Gets a guild by ID.
    ///
    /// This is an O(1) operation. This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn guild(&self, guild_id: GuildId) -> Option<Arc<CachedGuild>> {
        self.0.guilds.get(&guild_id).map(|r| Arc::clone(r.value()))
    }

    /// Gets the set of channels in a guild.
    ///
    /// This is a O(m) operation, where m is the amount of channels in the
    /// guild. This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn guild_channels(&self, guild_id: GuildId) -> Option<HashSet<ChannelId>> {
        self.0
            .guild_channels
            .get(&guild_id)
            .map(|r| r.value().clone())
    }

    /// Gets the set of presences in a guild.
    ///
    /// This list may be incomplete if not all members have been cached.
    ///
    /// This is a O(m) operation, where m is the amount of members in the guild.
    /// This requires the [`GUILD_PRESENCES`] intent.
    ///
    /// [`GUILD_PRESENCES`]: ::twilight_model::gateway::Intents::GUILD_PRESENCES
    pub fn guild_online(&self, guild_id: GuildId) -> Option<HashSet<UserId>> {
        self.0
            .guild_presences
            .get(&guild_id)
            .map(|r| r.value().clone())
    }

    /// Gets the set of roles in a guild.
    ///
    /// This is a O(m) operation, where m is the amount of roles in the guild.
    /// This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn guild_roles(&self, guild_id: GuildId) -> Option<HashSet<RoleId>> {
        self.0.guild_roles.get(&guild_id).map(|r| r.value().clone())
    }

    /// Gets a presence by, optionally, guild ID, and user ID.
    ///
    /// This is an O(1) operation. This requires the [`GUILD_PRESENCES`] intent.
    ///
    /// [`GUILD_PRESENCES`]: ::twilight_model::gateway::Intents::GUILD_PRESENCES
    pub fn presence(&self, guild_id: GuildId, user_id: UserId) -> bool {
        self.0
            .guild_presences
            .get(&guild_id)
            .map(|p| p.contains(&user_id))
            .unwrap_or(false)
    }

    /// Gets a private channel by ID.
    ///
    /// This is an O(1) operation. This requires the [`DIRECT_MESSAGES`] intent.
    ///
    /// [`DIRECT_MESSAGES`]: ::twilight_model::gateway::Intents::DIRECT_MESSAGES
    pub fn private_channel(&self, channel_id: ChannelId) -> Option<Arc<PrivateChannel>> {
        self.0
            .channels_private
            .get(&channel_id)
            .map(|r| Arc::clone(r.value()))
    }

    /// Gets a role by ID.
    ///
    /// This is an O(1) operation. This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn role(&self, role_id: RoleId) -> Option<Arc<Role>> {
        self.0
            .roles
            .get(&role_id)
            .map(|role| Arc::clone(&role.data))
    }

    /// Clear the state of the Cache.
    ///
    /// This is equal to creating a new empty cache.
    pub fn clear(&self) {
        self.0.channels_guild.clear();
        self.0.channels_private.clear();
        self.0
            .current_user
            .lock()
            .expect("current user poisoned")
            .take();
        self.0.groups.clear();
        self.0.guilds.clear();
        self.0.guild_channels.clear();
        self.0.guild_presences.clear();
        self.0.guild_roles.clear();
        self.0.roles.clear();
        self.0.unavailable_guilds.clear();
        self.0.voice_states.clear();
        self.0.pending_members.clear();
    }

    /// Gets the guild-level permissions for a given member.
    /// If the guild or any of the roles are not present, this will return
    /// Permissions::empty.
    pub fn guild_permissions<T>(
        &self,
        guild_id: GuildId,
        user_id: UserId,
        role_ids: T,
    ) -> Permissions
    where
        T: Iterator<Item = RoleId>,
    {
        // The owner has all permissions.
        if let Some(guild) = self.guild(guild_id) {
            if guild.owner_id == user_id {
                return Permissions::all();
            }
        }

        // The everyone role ID is the same as the guild ID.
        let everyone_perms = self
            .role(RoleId(guild_id.0))
            .map(|role| role.permissions)
            .unwrap_or_else(|| Permissions::empty());
        let perms = role_ids
            .map(|id| self.role(id))
            .filter_map(|role| role)
            .map(|role| role.permissions)
            .fold(everyone_perms, |acc, perm| acc | perm);

        // Administrators by default have every permission enabled.
        if perms.contains(Permissions::ADMINISTRATOR) {
            Permissions::all()
        } else {
            perms
        }
    }

    fn cache_current_user(&self, mut current_user: CurrentUser) {
        let mut user = self.0.current_user.lock().expect("current user poisoned");

        if let Some(mut user) = user.as_mut() {
            if let Some(user) = Arc::get_mut(&mut user) {
                std::mem::swap(user, &mut current_user);

                return;
            }
        }

        *user = Some(Arc::new(current_user));
    }

    fn cache_guild_channels(
        &self,
        guild_id: GuildId,
        guild_channels: impl IntoIterator<Item = GuildChannel>,
    ) {
        for channel in guild_channels {
            self.cache_guild_channel(guild_id, channel);
        }
    }

    fn cache_guild_channel(
        &self,
        guild_id: GuildId,
        mut channel: GuildChannel,
    ) -> Arc<CachedChannel> {
        match channel {
            GuildChannel::Category(ref mut c) => {
                c.guild_id.replace(guild_id);
            }
            GuildChannel::Text(ref mut c) => {
                c.guild_id.replace(guild_id);
            }
            GuildChannel::Voice(ref mut c) => {
                c.guild_id.replace(guild_id);
            }
        }

        let id = channel.id();
        self.0
            .guild_channels
            .entry(guild_id)
            .or_default()
            .insert(id);

        let cached_channel = CachedChannel {
            id: channel.id(),
            name: channel.name().to_owned(),
        };

        upsert_guild_item(&self.0.channels_guild, guild_id, id, cached_channel)
    }

    fn cache_group(&self, group: Group) -> Arc<Group> {
        upsert_item(&self.0.groups, group.id, group)
    }

    fn cache_guild(&self, guild: Guild) {
        // The map and set creation needs to occur first, so caching states and
        // objects always has a place to put them.
        if self.wants(ResourceType::CHANNEL) {
            self.0.guild_channels.insert(guild.id, HashSet::new());
            self.cache_guild_channels(guild.id, guild.channels);
        }

        if self.wants(ResourceType::MEMBER) {
            self.cache_members(guild.id, guild.members);
        }

        if self.wants(ResourceType::PRESENCE) {
            self.0.guild_presences.insert(guild.id, HashSet::new());
            self.cache_presences(guild.id, guild.presences);
        }

        if self.wants(ResourceType::ROLE) {
            self.0.guild_roles.insert(guild.id, HashSet::new());
            self.cache_roles(guild.id, guild.roles);
        }

        if self.wants(ResourceType::VOICE_STATE) {
            self.cache_voice_states(guild.id, guild.voice_states);
        }

        let guild = CachedGuild {
            id: guild.id,
            name: guild.name,
            description: guild.description,
            features: guild.features,
            icon: guild.icon,
            member_count: guild.member_count,
            owner_id: guild.owner_id,
            premium_subscription_count: guild.premium_subscription_count,
            premium_tier: guild.premium_tier,
            unavailable: guild.unavailable,
            vanity_url_code: guild.vanity_url_code,
        };

        self.0.unavailable_guilds.remove(&guild.id);
        self.0.guilds.insert(guild.id, Arc::new(guild));
    }

    fn cache_member(&self, guild_id: GuildId, member: &Member) {
        let id = (guild_id, member.user.id);
        if member.pending {
            self.0.pending_members.insert(id);
        } else {
            self.0.pending_members.remove(&id);
        }
    }

    fn cache_members(&self, guild_id: GuildId, members: impl IntoIterator<Item = Member>) {
        for member in members {
            self.cache_member(guild_id, &member);
        }
    }

    fn cache_presences(&self, guild_id: GuildId, presences: impl IntoIterator<Item = Presence>) {
        if let Some(mut kv) = self.0.guild_presences.get_mut(&guild_id) {
            for presence in presences {
                let user_id = presence_user_id(&presence);
                if presence.status == Status::Online {
                    kv.value_mut().insert(user_id);
                } else {
                    kv.value_mut().remove(&user_id);
                }
            }
        }
    }

    fn cache_presence(&self, guild_id: GuildId, user_id: UserId, status: Status) -> bool {
        let online = status == Status::Online;
        if let Some(mut kv) = self.0.guild_presences.get_mut(&guild_id) {
            if online {
                kv.value_mut().insert(user_id);
            } else {
                kv.value_mut().remove(&user_id);
            }
        }
        online
    }

    fn cache_private_channel(&self, private_channel: PrivateChannel) -> Arc<PrivateChannel> {
        let id = private_channel.id;

        match self.0.channels_private.get(&id) {
            Some(c) if **c == private_channel => Arc::clone(&c),
            Some(_) | None => {
                let v = Arc::new(private_channel);
                self.0.channels_private.insert(id, Arc::clone(&v));

                v
            }
        }
    }

    fn cache_roles(&self, guild_id: GuildId, roles: impl IntoIterator<Item = Role>) {
        for role in roles {
            self.cache_role(guild_id, role);
        }
    }

    fn cache_role(&self, guild_id: GuildId, role: Role) -> Arc<Role> {
        // Insert the role into the guild_roles map
        self.0
            .guild_roles
            .entry(guild_id)
            .or_default()
            .insert(role.id);

        // Insert the role into the all roles map
        upsert_guild_item(&self.0.roles, guild_id, role.id, role)
    }

    fn cache_voice_states(
        &self,
        guild_id: GuildId,
        voice_states: impl IntoIterator<Item = VoiceState>,
    ) {
        for voice_state in voice_states {
            self.cache_voice_state(guild_id, voice_state.user_id, voice_state.channel_id);
        }
    }

    fn cache_voice_state(&self, guild_id: GuildId, user_id: UserId, channel_id: Option<ChannelId>) {
        let key = (guild_id, user_id);
        match channel_id {
            Some(id) => {
                self.0.voice_states.insert(key, id);
            }
            None => {
                self.0.voice_states.remove(&key);
            }
        }
    }

    fn delete_group(&self, channel_id: ChannelId) -> Option<Arc<Group>> {
        self.0.groups.remove(&channel_id).map(|(_, v)| v)
    }

    fn unavailable_guild(&self, guild_id: GuildId) {
        self.0.unavailable_guilds.insert(guild_id);
        self.0.guilds.remove(&guild_id);
    }

    /// Delete a guild channel from the cache.
    ///
    /// The guild channel data itself and the channel entry in its guild's list
    /// of channels will be deleted.
    fn delete_guild_channel(&self, channel_id: ChannelId) -> Option<Arc<CachedChannel>> {
        let GuildItem { data, guild_id } = self.0.channels_guild.remove(&channel_id)?.1;

        if let Some(mut guild_channels) = self.0.guild_channels.get_mut(&guild_id) {
            guild_channels.remove(&channel_id);
        }

        Some(data)
    }

    fn delete_role(&self, role_id: RoleId) -> Option<Arc<Role>> {
        let role = self.0.roles.remove(&role_id).map(|(_, v)| v)?;

        if let Some(mut roles) = self.0.guild_roles.get_mut(&role.guild_id) {
            roles.remove(&role_id);
        }

        Some(role.data)
    }

    /// Determine whether the configured cache wants a specific resource to be
    /// processed.
    fn wants(&self, resource_type: ResourceType) -> bool {
        self.0.config.resource_types().contains(resource_type)
    }
}

pub fn presence_user_id(presence: &Presence) -> UserId {
    match presence.user {
        UserOrId::User(ref u) => u.id,
        UserOrId::UserId { id } => id,
    }
}

#[cfg(test)]
mod tests {
    use super::InMemoryCache;
    use std::borrow::Cow;
    use twilight_model::{
        channel::{ChannelType, GuildChannel, TextChannel},
        gateway::payload::{GuildEmojisUpdate, MemberRemove, RoleDelete},
        guild::{
            DefaultMessageNotificationLevel, Emoji, ExplicitContentFilter, Guild, Member, MfaLevel,
            Permissions, PremiumTier, Role, SystemChannelFlags, VerificationLevel,
        },
        id::{ChannelId, EmojiId, GuildId, RoleId, UserId},
        user::{CurrentUser, User},
        voice::VoiceState,
    };

    fn current_user(id: u64) -> CurrentUser {
        CurrentUser {
            avatar: None,
            bot: true,
            discriminator: "9876".to_owned(),
            email: None,
            id: UserId(id),
            mfa_enabled: true,
            name: "test".to_owned(),
            verified: Some(true),
            premium_type: None,
            public_flags: None,
            flags: None,
            locale: None,
        }
    }

    fn member(id: UserId, guild_id: GuildId) -> Member {
        Member {
            deaf: false,
            guild_id,
            hoisted_role: None,
            joined_at: None,
            mute: false,
            nick: None,
            pending: false,
            premium_since: None,
            roles: Vec::new(),
            user: user(id),
        }
    }

    fn role(id: RoleId) -> Role {
        Role {
            color: 0,
            hoist: false,
            id,
            managed: false,
            mentionable: false,
            name: "test".to_owned(),
            permissions: Permissions::empty(),
            position: 0,
            tags: None,
        }
    }

    fn user(id: UserId) -> User {
        User {
            avatar: None,
            bot: false,
            discriminator: "0001".to_owned(),
            email: None,
            flags: None,
            id,
            locale: None,
            mfa_enabled: None,
            name: "user".to_owned(),
            premium_type: None,
            public_flags: None,
            system: None,
            verified: None,
        }
    }

    /// Test retrieval of the current user, notably that it doesn't simply
    /// panic or do anything funny. This is the only synchronous mutex that we
    /// might have trouble with across await points if we're not careful.
    #[test]
    fn test_current_user_retrieval() {
        let cache = InMemoryCache::new();
        assert!(cache.current_user().is_none());
        cache.cache_current_user(current_user(1));
        assert!(cache.current_user().is_some());
    }

    #[test]
    fn test_syntax_update() {
        let cache = InMemoryCache::new();
        cache.update(&RoleDelete {
            guild_id: GuildId(0),
            role_id: RoleId(1),
        });
    }

    #[test]
    fn test_cache_role() {
        let cache = InMemoryCache::new();

        // Single inserts
        {
            // The role ids for the guild with id 1
            let guild_1_role_ids = (1..=10).map(RoleId).collect::<Vec<_>>();
            // Map the role ids to a test role
            let guild_1_roles = guild_1_role_ids
                .iter()
                .copied()
                .map(role)
                .collect::<Vec<_>>();
            // Cache all the roles using cache role
            for role in guild_1_roles.clone() {
                cache.cache_role(GuildId(1), role);
            }

            // Check for the cached guild role ids
            let cached_roles = cache.guild_roles(GuildId(1)).unwrap();
            assert_eq!(cached_roles.len(), guild_1_role_ids.len());
            assert!(guild_1_role_ids.iter().all(|id| cached_roles.contains(id)));

            // Check for the cached role
            assert!(guild_1_roles
                .into_iter()
                .all(|role| *cache.role(role.id).expect("Role missing from cache") == role))
        }

        // Bulk inserts
        {
            // The role ids for the guild with id 2
            let guild_2_role_ids = (101..=110).map(RoleId).collect::<Vec<_>>();
            // Map the role ids to a test role
            let guild_2_roles = guild_2_role_ids
                .iter()
                .copied()
                .map(role)
                .collect::<Vec<_>>();
            // Cache all the roles using cache roles
            cache.cache_roles(GuildId(2), guild_2_roles.clone());

            // Check for the cached guild role ids
            let cached_roles = cache.guild_roles(GuildId(2)).unwrap();
            assert_eq!(cached_roles.len(), guild_2_role_ids.len());
            assert!(guild_2_role_ids.iter().all(|id| cached_roles.contains(id)));

            // Check for the cached role
            assert!(guild_2_roles
                .into_iter()
                .all(|role| *cache.role(role.id).expect("Role missing from cache") == role))
        }
    }
}
