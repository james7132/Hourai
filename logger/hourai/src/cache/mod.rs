mod builder;
mod config;
mod updates;

pub use self::{
    builder::InMemoryCacheBuilder,
    config::{Config, ResourceType},
    updates::UpdateCache,
};

use dashmap::{DashMap, DashSet};
use std::{collections::HashSet, sync::Arc};
use twilight_model::{
    gateway::presence::{Presence, Status, UserOrId},
    guild::{Guild, Member},
    id::{ChannelId, GuildId, UserId},
    voice::VoiceState,
};

#[derive(Debug)]
struct GuildItem<T> {
    data: Arc<T>,
    guild_id: GuildId,
}

// When adding a field here, be sure to add it to `InMemoryCache::clear` if
// necessary.
#[derive(Debug, Default)]
struct InMemoryCacheRef {
    config: Arc<Config>,
    guilds: DashSet<GuildId>,
    guild_presences: DashMap<GuildId, HashSet<UserId>>,
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

    /// Checks if a member is pending in a speciifc guild.
    /// This runs O(1) time.
    pub fn is_pending(&self, guild_id: GuildId, user_id: UserId) -> bool {
        self.0.pending_members.contains(&(guild_id, user_id))
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

    /// Gets all of the IDs of the guilds in the cache.
    ///
    /// This is an O(n) operation. This requires the [`GUILDS`] intent.
    ///
    /// [`GUILDS`]: ::twilight_model::gateway::Intents::GUILDS
    pub fn guilds(&self) -> Vec<GuildId> {
        self.0.guilds.iter().map(|r| *r.key()).collect()
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

    /// Clear the state of the Cache.
    ///
    /// This is equal to creating a new empty cache.
    pub fn clear(&self) {
        self.0.guilds.clear();
        self.0.guild_presences.clear();
        self.0.unavailable_guilds.clear();
        self.0.voice_states.clear();
        self.0.pending_members.clear();
    }

    fn cache_guild(&self, guild: Guild) {
        // The map and set creation needs to occur first, so caching states and
        // objects always has a place to put them.
        if self.wants(ResourceType::MEMBER) {
            self.cache_members(guild.id, guild.members);
        }

        if self.wants(ResourceType::PRESENCE) {
            self.0.guild_presences.insert(guild.id, HashSet::new());
            self.cache_presences(guild.id, guild.presences);
        }

        if self.wants(ResourceType::VOICE_STATE) {
            self.cache_voice_states(guild.id, guild.voice_states);
        }

        self.0.unavailable_guilds.remove(&guild.id);
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

    fn unavailable_guild(&self, guild_id: GuildId) {
        self.0.unavailable_guilds.insert(guild_id);
        self.0.guilds.remove(&guild_id);
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
