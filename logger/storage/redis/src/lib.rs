mod compression;
mod guild_config;
mod keys;
pub mod modlog;
mod protobuf;

pub use redis::*;

use self::compression::Compressed;
pub use self::guild_config::CachedGuildConfig;
use self::keys::{CacheKey, GuildKey, Id};
use self::protobuf::Protobuf;
use anyhow::Result;
use hourai::{
    gateway::shard::ResumeSession,
    models::{
        channel::GuildChannel,
        guild::{Guild, PartialGuild, Permissions, Role},
        id::*,
        voice::VoiceState,
        MessageLike, Snowflake, UserLike,
    },
    proto::{cache::*, music_bot::MusicStateProto},
};
use redis::{FromRedisValue, ToRedisArgs};
use std::{
    cmp::{Ord, Ordering},
    collections::{HashMap, HashSet},
    hash::Hash,
    ops::Deref,
};
use tracing::debug;

type RedisPool = redis::aio::ConnectionManager;

pub async fn init(config: &hourai::config::HouraiConfig) -> RedisClient {
    debug!("Creating Redis client");
    let client = redis::Client::open(config.redis.as_ref()).expect("Failed to create Redis client");
    let pool = RedisPool::new(client)
        .await
        .expect("Failed to initialize multiplexed Redis connection");
    RedisClient::new(pool)
}

#[derive(Clone)]
pub struct RedisClient(RedisPool);

impl RedisClient {
    pub fn new(connection: RedisPool) -> Self {
        Self(connection)
    }

    pub fn connection(&self) -> &RedisPool {
        &self.0
    }

    pub fn connection_mut(&mut self) -> &mut RedisPool {
        &mut self.0
    }

    pub fn online_status(&self) -> OnlineStatus {
        OnlineStatus(self.clone())
    }

    pub fn guild(&self, guild_id: GuildId) -> GuildCache {
        GuildCache {
            guild_id,
            redis: self.clone(),
        }
    }

    pub fn guild_configs(&self) -> GuildConfig {
        GuildConfig(self.clone())
    }

    pub fn messages(&self) -> MessageCache {
        MessageCache(self.clone())
    }

    pub fn music_queues(&self) -> MusicQueues {
        MusicQueues(self.clone())
    }

    pub fn resume_states(&self) -> ResumeStates {
        ResumeStates(self.clone())
    }

    pub fn voice_states(&self) -> VoiceStateCache {
        VoiceStateCache(self.clone())
    }
}

pub struct OnlineStatus(RedisClient);

impl OnlineStatus {
    pub async fn set_online(
        &mut self,
        guild_id: GuildId,
        online: impl IntoIterator<Item = UserId>,
    ) -> Result<()> {
        let key = CacheKey::OnlineStatus(guild_id.get());
        let ids: Vec<Id<u64>> = online.into_iter().map(|id| Id(id.get())).collect();
        redis::pipe()
            .atomic()
            .del(key.clone())
            .ignore()
            .sadd(key.clone(), ids)
            .ignore()
            .expire(key.clone(), 3600)
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }

    pub async fn find_online(
        &mut self,
        guild_id: GuildId,
        users: impl IntoIterator<Item = UserId>,
    ) -> Result<HashSet<UserId>> {
        let key = CacheKey::OnlineStatus(guild_id.get());
        let user_ids: Vec<UserId> = users.into_iter().collect();
        let mut pipe = redis::pipe();
        user_ids.iter().map(|id| Id(id.get())).for_each(|id| {
            pipe.sismember(key.clone(), id);
        });
        let results: Vec<bool> = pipe.query_async(self.0.connection_mut()).await?;
        Ok(user_ids
            .into_iter()
            .zip(results)
            .filter(|(_, online)| *online)
            .map(|(id, _)| id)
            .collect())
    }
}

pub struct GuildConfig(RedisClient);

impl GuildConfig {
    pub async fn fetch<T: ::protobuf::Message + CachedGuildConfig>(
        &mut self,
        id: GuildId,
    ) -> Result<Option<T>> {
        let key = CacheKey::GuildConfigs(id.get());
        let response: Option<Compressed<Protobuf<T>>> = redis::Cmd::hget(key, vec![T::SUBKEY])
            .query_async(self.0.connection_mut())
            .await?;
        Ok(response.map(|c| c.0 .0))
    }

    pub async fn fetch_or_default<T: ::protobuf::Message + CachedGuildConfig>(
        &mut self,
        id: GuildId,
    ) -> Result<T> {
        Ok(self.fetch::<T>(id).await?.unwrap_or_else(T::new))
    }

    pub async fn set<T: ::protobuf::Message + CachedGuildConfig>(
        &mut self,
        id: GuildId,
        value: T,
    ) -> Result<()> {
        let key = CacheKey::GuildConfigs(id.get());
        redis::Cmd::hset(key, vec![T::SUBKEY], Compressed(Protobuf(value)))
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }
}

pub struct MessageCache(RedisClient);

impl MessageCache {
    pub async fn cache(&mut self, message: impl MessageLike) -> Result<()> {
        let mut msg = CachedMessageProto::new();
        msg.set_content(message.content().to_owned());
        if let Some(guild_id) = message.guild_id() {
            msg.set_guild_id(guild_id.get())
        }

        let user = msg.mut_author();
        let author = message.author();
        user.set_id(author.id().get());
        user.set_username(author.name().to_owned());
        user.set_discriminator(author.discriminator() as u32);
        user.set_bot(author.bot());
        if let Some(avatar) = author.avatar_hash() {
            user.set_avatar(avatar.to_owned());
        }

        let key = CacheKey::Messages(message.id().get(), message.channel_id().get());
        // Keep 1 day's worth of messages cached.
        redis::Cmd::set_ex(key, Protobuf(msg), 86400)
            .query_async(self.0.connection_mut())
            .await?;

        Ok(())
    }

    pub async fn fetch(
        &mut self,
        channel_id: ChannelId,
        message_id: MessageId,
    ) -> Result<Option<CachedMessageProto>> {
        let key = CacheKey::Messages(channel_id.get(), message_id.get());
        let proto: Option<Protobuf<CachedMessageProto>> = redis::Cmd::get(key)
            .query_async(self.0.connection_mut())
            .await?;
        Ok(proto.map(|msg| {
            let mut proto = msg.0;
            proto.set_id(message_id.get());
            proto.set_channel_id(channel_id.get());
            proto
        }))
    }

    pub async fn delete(&mut self, channel_id: ChannelId, id: MessageId) -> Result<()> {
        self.bulk_delete(channel_id, vec![id]).await
    }

    pub async fn bulk_delete(
        &mut self,
        channel_id: ChannelId,
        ids: impl IntoIterator<Item = MessageId>,
    ) -> Result<()> {
        let keys: Vec<CacheKey> = ids
            .into_iter()
            .map(|id| CacheKey::Messages(channel_id.get(), id.get()))
            .collect();
        redis::Cmd::del(keys)
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }
}

pub struct VoiceStateCache(RedisClient);

impl VoiceStateCache {
    pub async fn update_guild(&mut self, guild: &Guild) -> Result<()> {
        let mut pipe = redis::pipe();
        pipe.atomic()
            .del(CacheKey::VoiceState(guild.id.get()))
            .ignore();
        for state in guild.voice_states.iter() {
            pipe.add_command(Self::save_cmd(state)).ignore();
        }
        pipe.query_async(self.0.connection_mut()).await?;
        Ok(())
    }

    pub async fn get_channel(
        &mut self,
        guild_id: GuildId,
        user_id: UserId,
    ) -> Result<Option<ChannelId>> {
        let channel_id: Option<u64> =
            redis::Cmd::hget(CacheKey::VoiceState(guild_id.get()), user_id.get())
                .query_async(self.0.connection_mut())
                .await?;
        Ok(channel_id.and_then(ChannelId::new))
    }

    pub async fn get_channels(&mut self, guild_id: GuildId) -> Result<HashMap<UserId, ChannelId>> {
        let all_users: HashMap<u64, u64> =
            redis::Cmd::hgetall(CacheKey::VoiceState(guild_id.get()))
                .query_async(self.0.connection_mut())
                .await?;
        Ok(all_users
            .into_iter()
            .filter_map(|(user, channel)| {
                UserId::new(user).and_then(|u| ChannelId::new(channel).map(|ch| (u, ch)))
            })
            .collect())
    }

    pub async fn save(&mut self, state: &VoiceState) -> Result<()> {
        Self::save_cmd(state)
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }

    pub async fn clear_guild(&mut self, guild_id: GuildId) -> Result<()> {
        redis::Cmd::del(CacheKey::VoiceState(guild_id.get()))
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }

    fn save_cmd(state: &VoiceState) -> redis::Cmd {
        let guild_id = state
            .guild_id
            .expect("Only voice states in guilds should be cached");
        let key = CacheKey::VoiceState(guild_id.get());
        if let Some(channel_id) = state.channel_id {
            redis::Cmd::hset(key, state.user_id.get(), channel_id.get())
        } else {
            redis::Cmd::hdel(key, state.user_id.get())
        }
    }
}

pub struct GuildCache {
    guild_id: GuildId,
    redis: RedisClient,
}

impl GuildCache {
    pub async fn save(&mut self, guild: &hourai::models::guild::Guild) -> Result<()> {
        assert!(self.guild_id == guild.id);
        let key = CacheKey::Guild(guild.id.get());
        let mut pipe = redis::pipe();
        pipe.atomic().del(key).ignore();
        pipe.add_command(self.save_resource_cmd(guild.id, guild))
            .ignore();
        for channel in guild.channels.iter() {
            pipe.add_command(self.save_resource_cmd(channel.id(), channel))
                .ignore();
        }
        for role in guild.roles.iter() {
            pipe.add_command(self.save_resource_cmd(role.id, role))
                .ignore();
        }
        pipe.query_async(self.redis.connection_mut()).await?;
        Ok(())
    }

    /// Deletes all of the cached information about a guild from the cache.
    pub async fn delete(&mut self) -> Result<()> {
        redis::Cmd::del(CacheKey::Guild(self.guild_id.get()))
            .query_async(self.redis.connection_mut())
            .await?;
        Ok(())
    }

    /// Gets a cached resource from the cache.
    pub async fn fetch_resource<T: GuildResource>(
        &mut self,
        resource_id: T::Id,
    ) -> Result<Option<T::Proto>>
    where
        GuildKey: From<T::Id> + ToRedisArgs,
    {
        let guild_key = CacheKey::Guild(self.guild_id.get());
        let proto: Option<Protobuf<T::Proto>> = redis::Cmd::hget(guild_key, resource_id.into())
            .query_async(self.redis.connection_mut())
            .await?;
        Ok(proto.map(|proto| proto.0))
    }

    /// Fetches multiple resources from the cache.
    pub async fn fetch_all_resources<T: GuildResource>(
        &mut self,
    ) -> Result<HashMap<T::Id, T::Proto>>
    where
        GuildKey: From<T::Id> + ToRedisArgs,
    {
        // TODO(james7132): Using HGETALL here is super inefficient with guilds with high
        // role/channel counts, see if this is avoidable.
        let guild_key = CacheKey::Guild(self.guild_id.get());
        let response: HashMap<GuildKey, redis::Value> = redis::Cmd::hgetall(guild_key)
            .query_async(self.redis.connection_mut())
            .await?;
        let mut protos = HashMap::new();
        for (key, value) in response.into_iter() {
            if key.prefix() != T::PREFIX {
                continue;
            }
            let proto = Protobuf::<T::Proto>::from_redis_value(&value)?;
            protos.insert(T::from_key(key), proto.0);
        }

        Ok(protos)
    }

    /// Fetches multiple resources from the cache.
    pub async fn fetch_resources<T: GuildResource>(
        &mut self,
        resource_ids: &[T::Id],
    ) -> Result<Vec<T::Proto>>
    where
        GuildKey: From<T::Id> + ToRedisArgs,
    {
        Ok(match resource_ids.len() {
            0 => vec![],
            1 => self
                .fetch_resource::<T>(resource_ids[0])
                .await?
                .into_iter()
                .collect(),
            _ => {
                let guild_key = CacheKey::Guild(self.guild_id.get());
                let resource_keys: Vec<GuildKey> =
                    resource_ids.iter().map(|id| id.clone().into()).collect();
                let protos: Vec<Option<Protobuf<T::Proto>>> =
                    redis::Cmd::hget(guild_key, resource_keys)
                        .query_async(self.redis.connection_mut())
                        .await?;
                protos
                    .into_iter()
                    .filter_map(|p| p.map(|proto| proto.0))
                    .collect()
            }
        })
    }

    /// Saves a resoruce into the cache.
    pub async fn save_resource<T: GuildResource>(
        &mut self,
        resource_id: T::Id,
        data: &T,
    ) -> Result<()>
    where
        GuildKey: From<T::Id> + ToRedisArgs,
    {
        let proto = Protobuf(data.to_proto());
        self.redis
            .connection_mut()
            .hset(
                CacheKey::Guild(self.guild_id.get()),
                resource_id.into(),
                proto,
            )
            .await?;
        Ok(())
    }

    fn save_resource_cmd<T: GuildResource>(&self, resource_id: T::Id, data: &T) -> redis::Cmd
    where
        GuildKey: From<T::Id> + ToRedisArgs,
    {
        let proto = Protobuf(data.to_proto());
        redis::Cmd::hset(
            CacheKey::Guild(self.guild_id.get()),
            resource_id.into(),
            proto,
        )
    }

    /// Deletes a resource from the cache.
    pub async fn delete_resource<T: GuildResource>(&mut self, resource_id: T::Id) -> Result<()>
    where
        GuildKey: From<T::Id> + ToRedisArgs,
    {
        self.redis
            .connection_mut()
            .hdel(CacheKey::Guild(self.guild_id.get()), resource_id.into())
            .await?;
        Ok(())
    }

    /// Fetches a `RoleSet` from the provided guild and role IDs.
    pub async fn role_set(&mut self, role_ids: &[RoleId]) -> Result<RoleSet> {
        Ok(RoleSet(self.fetch_resources::<Role>(role_ids).await?))
    }

    /// Gets the guild-level permissions for a given member.
    /// If the guild or any of the roles are not present, this will return
    /// Permissions::empty.
    pub async fn guild_permissions(
        &mut self,
        user_id: UserId,
        role_ids: impl Iterator<Item = RoleId>,
    ) -> Result<Permissions> {
        // The owner has all permissions.
        if let Some(guild) = self.fetch_resource::<Guild>(self.guild_id).await? {
            if guild.get_owner_id() == user_id.get() {
                return Ok(Permissions::all());
            }
        } else {
            return Ok(Permissions::empty());
        }

        // The everyone role ID is the same as the guild ID.
        let mut role_ids: Vec<RoleId> = role_ids.collect();
        role_ids.push(RoleId(self.guild_id.0));
        Ok(self.role_set(&role_ids).await?.guild_permissions())
    }
}

#[derive(Clone, Eq, PartialEq)]
pub struct RoleSet(Vec<CachedRoleProto>);

impl RoleSet {
    /// Gets the highest role in the `RoleSet`, if available. Returns None if the set is empty.
    pub fn highest(&self) -> Option<&CachedRoleProto> {
        self.0.iter().max()
    }

    /// Computes the available permissions for all of the roles.
    pub fn guild_permissions(&self) -> Permissions {
        let perms = self
            .0
            .iter()
            .map(|role| Permissions::from_bits_truncate(role.get_permissions()))
            .fold(Permissions::empty(), |acc, perm| acc | perm);

        // Administrators by default have every permission enabled.
        if perms.contains(Permissions::ADMINISTRATOR) {
            Permissions::all()
        } else {
            perms
        }
    }
}

impl Ord for RoleSet {
    fn cmp(&self, other: &Self) -> Ordering {
        match (self.highest(), other.highest()) {
            (Some(left), Some(right)) => left.cmp(&right),
            (Some(_), None) => Ordering::Greater,
            (None, Some(_)) => Ordering::Less,
            (None, None) => Ordering::Equal,
        }
    }
}

impl PartialOrd for RoleSet {
    fn partial_cmp(&self, other: &Self) -> Option<Ordering> {
        Some(self.cmp(other))
    }
}

impl Deref for RoleSet {
    type Target = Vec<CachedRoleProto>;
    fn deref(&self) -> &Self::Target {
        &self.0
    }
}

pub trait ToProto {
    type Proto: ::protobuf::Message;
    fn to_proto(&self) -> Self::Proto;
}

pub trait GuildResource: ToProto {
    type Id: Into<GuildKey> + Copy + Eq + Hash;
    type Subkey;
    const PREFIX: u8;

    fn from_key(id: GuildKey) -> Self::Id;
}

impl GuildResource for Guild {
    type Id = GuildId;
    type Subkey = ();
    const PREFIX: u8 = 1_u8;

    fn from_key(_: GuildKey) -> Self::Id {
        panic!("Converting GuildKey to GuildId is not supported");
    }
}

impl ToProto for Guild {
    type Proto = CachedGuildProto;
    fn to_proto(&self) -> Self::Proto {
        let mut proto = Self::Proto::new();
        proto.set_id(self.id.get());
        proto.set_name(self.name.clone());
        proto.features = ::protobuf::RepeatedField::from_vec(self.features.clone());
        proto.set_owner_id(self.owner_id.get());
        if let Some(ref code) = self.vanity_url_code {
            proto.set_vanity_url_code(code.clone());
        }
        proto
    }
}

impl GuildResource for PartialGuild {
    type Id = GuildId;
    type Subkey = ();
    const PREFIX: u8 = 1_u8;

    fn from_key(_: GuildKey) -> Self::Id {
        panic!("Converting GuildKey to GuildId is not supported");
    }
}

impl ToProto for PartialGuild {
    type Proto = CachedGuildProto;
    fn to_proto(&self) -> Self::Proto {
        let mut proto = Self::Proto::new();
        proto.set_id(self.id.get());
        proto.set_name(self.name.clone());
        proto.features = ::protobuf::RepeatedField::from_vec(self.features.clone());
        proto.set_owner_id(self.owner_id.get());
        if let Some(ref code) = self.vanity_url_code {
            proto.set_vanity_url_code(code.clone());
        }
        proto
    }
}

impl GuildResource for GuildChannel {
    type Id = ChannelId;
    type Subkey = u64;
    const PREFIX: u8 = 3_u8;

    fn from_key(key: GuildKey) -> Self::Id {
        if let GuildKey::Channel(id) = key {
            id
        } else {
            panic!("Invalid GuildKey for channel: {:?}", key);
        }
    }
}

impl ToProto for GuildChannel {
    type Proto = CachedGuildChannelProto;
    fn to_proto(&self) -> Self::Proto {
        let mut proto = Self::Proto::new();
        proto.set_channel_id(self.id().get());
        proto.set_name(self.name().to_owned());
        proto
    }
}

impl GuildResource for Role {
    type Id = RoleId;
    type Subkey = u64;
    const PREFIX: u8 = 2_u8;

    fn from_key(key: GuildKey) -> Self::Id {
        if let GuildKey::Role(id) = key {
            id
        } else {
            panic!("Invalid GuildKey for channel: {:?}", key);
        }
    }
}

impl ToProto for Role {
    type Proto = CachedRoleProto;
    fn to_proto(&self) -> Self::Proto {
        let mut proto = Self::Proto::new();
        proto.set_role_id(self.id.get());
        proto.set_name(self.name.clone());
        proto.set_position(self.position);
        proto.set_permissions(self.permissions.bits());
        proto
    }
}

pub struct ResumeStates(RedisClient);

impl ResumeStates {
    pub async fn save_sessions(
        &mut self,
        key: &str,
        sessions: HashMap<u64, ResumeSession>,
    ) -> Result<()> {
        let sessions: Vec<(u64, String)> = sessions
            .into_iter()
            .filter_map(|(shard, session)| serde_json::to_string(&session).map(|s| (shard, s)).ok())
            .collect();
        redis::Cmd::hset_multiple(CacheKey::ResumeState(key.into()), &sessions)
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }

    pub async fn get_sessions(&mut self, key: &str) -> HashMap<u64, ResumeSession> {
        let sessions = redis::Cmd::hgetall(CacheKey::ResumeState(key.into()))
            .query_async::<RedisPool, HashMap<u64, String>>(self.0.connection_mut())
            .await;
        if let Ok(sessions) = sessions {
            sessions
                .into_iter()
                .filter_map(|(shard, session)| {
                    serde_json::from_str(&session).map(|s| (shard, s)).ok()
                })
                .collect()
        } else {
            HashMap::new()
        }
    }
}

pub struct MusicQueues(RedisClient);

impl MusicQueues {
    pub async fn save(&mut self, guild_id: GuildId, state: MusicStateProto) -> Result<()> {
        redis::Cmd::set(CacheKey::MusicQueue(guild_id.get()), Protobuf(state))
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }

    pub async fn has_saved_state(&mut self, guild_id: GuildId) -> Result<bool> {
        let present: bool = redis::Cmd::exists(CacheKey::MusicQueue(guild_id.get()))
            .query_async(self.0.connection_mut())
            .await?;
        Ok(present)
    }

    pub async fn load(&mut self, guild_id: GuildId) -> Result<MusicStateProto> {
        let state: Protobuf<MusicStateProto> =
            redis::Cmd::get(CacheKey::MusicQueue(guild_id.get()))
                .query_async(self.0.connection_mut())
                .await?;
        Ok(state.0)
    }

    pub async fn clear(&mut self, guild_id: GuildId) -> Result<()> {
        redis::Cmd::del(CacheKey::MusicQueue(guild_id.get()))
            .query_async(self.0.connection_mut())
            .await?;
        Ok(())
    }
}
