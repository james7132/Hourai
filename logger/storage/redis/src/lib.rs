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
        channel::Channel,
        guild::{Guild, PartialGuild, Permissions, Role},
        id::{marker::*, Id as TwilightId},
        voice::VoiceState,
        MessageLike, Snowflake, UserLike,
    },
    proto::{cache::*, music_bot::MusicStateProto},
};
use redis::{FromRedisValue, ToRedisArgs};
use std::{
    cmp::{Ord, Ordering},
    collections::{HashMap, HashSet},
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

    pub fn guild(&self, guild_id: TwilightId<GuildMarker>) -> GuildCache {
        GuildCache {
            guild_id,
            redis: self.clone(),
        }
    }

    pub fn messages(&self) -> MessageCache {
        MessageCache(self.clone())
    }

    pub fn resume_states(&self) -> ResumeStates {
        ResumeStates(self.clone())
    }
}

pub struct OnlineStatus(RedisClient);

impl OnlineStatus {
    pub async fn set_online(
        &mut self,
        guild_id: TwilightId<GuildMarker>,
        online: impl IntoIterator<Item = TwilightId<UserMarker>>,
    ) -> Result<()> {
        let key = CacheKey::OnlineStatus(guild_id);
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
        guild_id: TwilightId<GuildMarker>,
        users: impl IntoIterator<Item = TwilightId<UserMarker>>,
    ) -> Result<HashSet<TwilightId<UserMarker>>> {
        let key = CacheKey::OnlineStatus(guild_id);
        let user_ids: Vec<TwilightId<UserMarker>> = users.into_iter().collect();
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

pub struct GuildConfig(GuildCache);

impl GuildConfig {
    pub async fn fetch<T: ::protobuf::Message + CachedGuildConfig>(&mut self) -> Result<Option<T>> {
        let key = CacheKey::GuildConfigs(self.0.guild_id);
        let response: Option<Compressed<Protobuf<T>>> = self
            .0
            .redis
            .connection_mut()
            .hget(key, vec![T::SUBKEY])
            .await?;
        Ok(response.map(|c| c.0 .0))
    }

    pub async fn get<T: ::protobuf::Message + CachedGuildConfig>(&mut self) -> Result<T> {
        Ok(self.fetch::<T>().await?.unwrap_or_else(T::new))
    }

    pub async fn set<T: ::protobuf::Message + CachedGuildConfig>(
        &mut self,
        value: T,
    ) -> Result<()> {
        let key = CacheKey::GuildConfigs(self.0.guild_id);
        self.0
            .redis
            .connection_mut()
            .hset(key, vec![T::SUBKEY], Compressed(Protobuf(value)))
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
            user.set_avatar(avatar.to_string());
        }

        let key = CacheKey::Messages(message.channel_id(), message.id());
        // Keep 1 day's worth of messages cached.
        self.0
            .connection_mut()
            .set_ex(key, Protobuf(msg), 86400)
            .await?;

        Ok(())
    }

    pub async fn fetch(
        &mut self,
        channel_id: TwilightId<ChannelMarker>,
        message_id: TwilightId<MessageMarker>,
    ) -> Result<Option<CachedMessageProto>> {
        let key = CacheKey::Messages(channel_id, message_id);
        let proto: Option<Protobuf<CachedMessageProto>> = self.0.connection_mut().get(key).await?;
        Ok(proto.map(|msg| {
            let mut proto = msg.0;
            proto.set_id(message_id.get());
            proto.set_channel_id(channel_id.get());
            proto
        }))
    }

    pub async fn delete(
        &mut self,
        channel_id: TwilightId<ChannelMarker>,
        id: TwilightId<MessageMarker>,
    ) -> Result<()> {
        self.bulk_delete(channel_id, vec![id]).await
    }

    pub async fn bulk_delete(
        &mut self,
        channel_id: TwilightId<ChannelMarker>,
        ids: impl IntoIterator<Item = TwilightId<MessageMarker>>,
    ) -> Result<()> {
        let keys: Vec<CacheKey> = ids
            .into_iter()
            .map(|id| CacheKey::Messages(channel_id, id))
            .collect();
        self.0.connection_mut().del(keys).await?;
        Ok(())
    }
}

pub struct VoiceStateCache(GuildCache);

impl VoiceStateCache {
    pub async fn update_guild(&mut self, guild: &Guild) -> Result<()> {
        assert!(self.0.guild_id == guild.id);
        let mut pipe = redis::pipe();
        pipe.atomic().del(CacheKey::VoiceState(guild.id)).ignore();
        for state in guild.voice_states.iter() {
            pipe.add_command(self.save_cmd(state)).ignore();
        }
        pipe.query_async(self.0.redis.connection_mut()).await?;
        Ok(())
    }

    pub async fn get_channel(
        &mut self,
        user_id: TwilightId<UserMarker>,
    ) -> Result<Option<TwilightId<ChannelMarker>>> {
        let channel_id: Option<u64> = self
            .0
            .redis
            .connection_mut()
            .hget(CacheKey::VoiceState(self.0.guild_id), user_id.get())
            .await?;
        Ok(channel_id.map(TwilightId::new))
    }

    pub async fn get_channels(
        &mut self,
    ) -> Result<HashMap<TwilightId<UserMarker>, TwilightId<ChannelMarker>>> {
        let all_users: HashMap<u64, u64> = self
            .0
            .redis
            .connection_mut()
            .hgetall(CacheKey::VoiceState(self.0.guild_id))
            .await?;
        Ok(all_users
            .into_iter()
            .map(|(user, channel)| (TwilightId::new(user), TwilightId::new(channel)))
            .collect())
    }

    pub async fn save(&mut self, state: &VoiceState) -> Result<()> {
        self.save_cmd(state)
            .query_async(self.0.redis.connection_mut())
            .await?;
        Ok(())
    }

    pub async fn clear(&mut self) -> Result<()> {
        self.0
            .redis
            .connection_mut()
            .del(CacheKey::VoiceState(self.0.guild_id))
            .await?;
        Ok(())
    }

    fn save_cmd(&self, state: &VoiceState) -> redis::Cmd {
        let key = CacheKey::VoiceState(self.0.guild_id);
        if let Some(channel_id) = state.channel_id {
            redis::Cmd::hset(key, state.user_id.get(), channel_id.get())
        } else {
            redis::Cmd::hdel(key, state.user_id.get())
        }
    }
}

#[derive(Clone)]
pub struct GuildCache {
    guild_id: TwilightId<GuildMarker>,
    redis: RedisClient,
}

impl GuildCache {
    pub fn configs(&self) -> GuildConfig {
        GuildConfig(self.clone())
    }

    pub fn music_queue(&self) -> MusicQueues {
        MusicQueues(self.clone())
    }

    pub fn voice_states(&self) -> VoiceStateCache {
        VoiceStateCache(self.clone())
    }

    pub async fn save(&mut self, guild: &hourai::models::guild::Guild) -> Result<()> {
        assert!(self.guild_id == guild.id);
        let key = CacheKey::Guild(guild.id);
        let mut pipe = redis::pipe();
        pipe.atomic().del(key).ignore();
        pipe.add_command(self.save_resource_cmd(guild.id, guild))
            .ignore();
        for channel in guild.channels.iter() {
            pipe.add_command(self.save_resource_cmd(channel.id, channel))
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
        self.redis
            .connection_mut()
            .del(CacheKey::Guild(self.guild_id))
            .await?;
        Ok(())
    }

    /// Gets a cached resource from the cache.
    pub async fn fetch_resource<T: GuildResource>(
        &mut self,
        resource_id: TwilightId<T::Marker>,
    ) -> Result<Option<T::Proto>>
    where
        GuildKey: From<TwilightId<T::Marker>> + ToRedisArgs,
    {
        let guild_key = CacheKey::Guild(self.guild_id);
        let resource_key = GuildKey::from(resource_id);
        let proto: Option<Protobuf<T::Proto>> = self
            .redis
            .connection_mut()
            .hget(guild_key, resource_key)
            .await?;
        Ok(proto.map(|proto| proto.0))
    }

    /// Fetches multiple resources from the cache.
    pub async fn fetch_all_resources<T: GuildResource>(
        &mut self,
    ) -> Result<HashMap<TwilightId<T::Marker>, T::Proto>>
    where
        GuildKey: From<TwilightId<T::Marker>> + ToRedisArgs,
    {
        // TODO(james7132): Using HGETALL here is super inefficient with guilds with high
        // role/channel counts, see if this is avoidable.
        let guild_key = CacheKey::Guild(self.guild_id);
        let response: HashMap<GuildKey, redis::Value> =
            self.redis.connection_mut().hgetall(guild_key).await?;
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
        resource_ids: &[TwilightId<T::Marker>],
    ) -> Result<Vec<T::Proto>>
    where
        T::Marker: Clone,
        GuildKey: From<TwilightId<T::Marker>> + ToRedisArgs,
    {
        Ok(match resource_ids.len() {
            0 => vec![],
            1 => self
                .fetch_resource::<T>(resource_ids[0].clone())
                .await?
                .into_iter()
                .collect(),
            _ => {
                let guild_key = CacheKey::Guild(self.guild_id);
                let resource_keys: Vec<_> = resource_ids
                    .iter()
                    .map(|id| GuildKey::from(id.clone()))
                    .collect();
                let protos: Vec<Option<Protobuf<T::Proto>>> = self
                    .redis
                    .connection_mut()
                    .hget(guild_key, resource_keys)
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
        resource_id: TwilightId<T::Marker>,
        data: &T,
    ) -> Result<()>
    where
        GuildKey: From<TwilightId<T::Marker>> + ToRedisArgs,
    {
        let guild_key = CacheKey::Guild(self.guild_id);
        let resource_key = GuildKey::from(resource_id);
        let proto = Protobuf(data.to_proto());
        self.redis
            .connection_mut()
            .hset(guild_key, resource_key, proto)
            .await?;
        Ok(())
    }

    fn save_resource_cmd<T: GuildResource>(
        &self,
        resource_id: TwilightId<T::Marker>,
        data: &T,
    ) -> redis::Cmd
    where
        GuildKey: From<TwilightId<T::Marker>> + ToRedisArgs,
    {
        let guild_key = CacheKey::Guild(self.guild_id);
        let resource_key = GuildKey::from(resource_id);
        let proto = Protobuf(data.to_proto());
        redis::Cmd::hset(guild_key, resource_key, proto)
    }

    /// Deletes a resource from the cache.
    pub async fn delete_resource<T: GuildResource>(
        &mut self,
        resource_id: TwilightId<T::Marker>,
    ) -> Result<()>
    where
        GuildKey: From<TwilightId<T::Marker>> + ToRedisArgs,
    {
        let guild_key = CacheKey::Guild(self.guild_id);
        let resource_key = GuildKey::from(resource_id);
        self.redis
            .connection_mut()
            .hdel(guild_key, resource_key)
            .await?;
        Ok(())
    }

    /// Fetches a `RoleSet` from the provided guild and role IDs.
    pub async fn role_set(&mut self, role_ids: &[TwilightId<RoleMarker>]) -> Result<RoleSet> {
        Ok(RoleSet(self.fetch_resources::<Role>(role_ids).await?))
    }

    /// Gets the guild-level permissions for a given member.
    /// If the guild or any of the roles are not present, this will return
    /// Permissions::empty.
    pub async fn guild_permissions(
        &mut self,
        user_id: TwilightId<UserMarker>,
        role_ids: impl Iterator<Item = TwilightId<RoleMarker>>,
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
        let mut role_ids: Vec<TwilightId<RoleMarker>> = role_ids.collect();
        role_ids.push(self.guild_id.cast());
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
    type Marker;
    type Subkey;
    const PREFIX: u8;

    fn from_key(id: GuildKey) -> TwilightId<Self::Marker>;
}

impl GuildResource for Guild {
    type Marker = GuildMarker;
    type Subkey = ();
    const PREFIX: u8 = 1_u8;

    fn from_key(_: GuildKey) -> TwilightId<Self::Marker> {
        panic!("Converting GuildKey to Id<GuildMarker> is not supported");
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
    type Marker = GuildMarker;
    type Subkey = ();
    const PREFIX: u8 = 1_u8;

    fn from_key(_: GuildKey) -> TwilightId<Self::Marker> {
        panic!("Converting GuildKey to Id<GuildMarker> is not supported");
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

impl GuildResource for Channel {
    type Marker = ChannelMarker;
    type Subkey = u64;
    const PREFIX: u8 = 3_u8;

    fn from_key(key: GuildKey) -> TwilightId<Self::Marker> {
        if let GuildKey::Channel(id) = key {
            id
        } else {
            panic!("Invalid GuildKey for channel: {:?}", key);
        }
    }
}

impl ToProto for Channel {
    type Proto = CachedGuildChannelProto;
    fn to_proto(&self) -> Self::Proto {
        assert!(self.guild_id.is_some());
        let mut proto = Self::Proto::new();
        proto.set_channel_id(self.id.get());
        proto.set_name(self.name.as_ref().unwrap().to_owned());
        proto
    }
}

impl GuildResource for Role {
    type Marker = RoleMarker;
    type Subkey = u64;
    const PREFIX: u8 = 2_u8;

    fn from_key(key: GuildKey) -> TwilightId<Self::Marker> {
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
        self.0
            .connection_mut()
            .hset_multiple(CacheKey::ResumeState(key.into()), &sessions)
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

pub struct MusicQueues(GuildCache);

impl MusicQueues {
    pub async fn save(&mut self, state: MusicStateProto) -> Result<()> {
        self.0
            .redis
            .connection_mut()
            .set(CacheKey::MusicQueue(self.0.guild_id), Protobuf(state))
            .await?;
        Ok(())
    }

    pub async fn has_saved_state(&mut self) -> Result<bool> {
        let present: bool = self
            .0
            .redis
            .connection_mut()
            .exists(CacheKey::MusicQueue(self.0.guild_id))
            .await?;
        Ok(present)
    }

    pub async fn load(&mut self) -> Result<MusicStateProto> {
        let state: Protobuf<MusicStateProto> = self
            .0
            .redis
            .connection_mut()
            .get(CacheKey::MusicQueue(self.0.guild_id))
            .await?;
        Ok(state.0)
    }

    pub async fn clear(&mut self) -> Result<()> {
        self.0
            .redis
            .connection_mut()
            .del(CacheKey::MusicQueue(self.0.guild_id))
            .await?;
        Ok(())
    }
}
