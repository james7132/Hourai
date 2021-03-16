mod compression;
mod guild_config;
mod keys;
mod protobuf;

use self::compression::Compressed;
pub use self::guild_config::CachedGuildConfig;
use self::keys::{CacheKey, CachePrefix, Id};
use self::protobuf::Protobuf;
use anyhow::Result;
use hourai::models::{id::*, MessageLike, Snowflake, UserLike};
use hourai::proto::cache::*;
use redis::aio::ConnectionLike;
use tracing::debug;

pub type RedisPool = redis::aio::ConnectionManager;

pub async fn init(config: &hourai::config::HouraiConfig) -> RedisPool {
    debug!("Creating Redis client");
    let client = redis::Client::open(config.redis.as_ref()).expect("Failed to create Redis client");
    RedisPool::new(client)
        .await
        .expect("Failed to initialize multiplexed Redis connection")
}

pub struct OnlineStatus {
    pipeline: redis::Pipeline,
}

impl Default for OnlineStatus {
    fn default() -> Self {
        Self {
            pipeline: redis::pipe().atomic().clone(),
        }
    }
}

impl OnlineStatus {
    pub fn new() -> Self {
        Self::default()
    }

    pub fn set_online(
        &mut self,
        guild_id: GuildId,
        online: impl IntoIterator<Item = UserId>,
    ) -> &mut Self {
        let key = CacheKey(CachePrefix::OnlineStatus, guild_id.0);
        let ids: Vec<Id<u64>> = online.into_iter().map(|id| Id(id.0)).collect();
        self.pipeline
            .del(key)
            .ignore()
            .sadd(key, ids)
            .ignore()
            .expire(key, 3600);
        self
    }

    pub fn build(self) -> redis::Pipeline {
        self.pipeline
    }
}

pub struct GuildConfig;

impl GuildConfig {
    pub async fn fetch<T: ::protobuf::Message + CachedGuildConfig>(
        id: GuildId,
        conn: &mut RedisPool,
    ) -> std::result::Result<Option<T>, redis::RedisError> {
        let key = CacheKey(CachePrefix::GuildConfigs, id.0);
        let response: Option<Compressed<Protobuf<T>>> = redis::Cmd::hget(key, vec![T::SUBKEY])
            .query_async(conn)
            .await?;
        Ok(response.map(|c| c.0.0))
    }

    pub async fn fetch_or_default<T: ::protobuf::Message + CachedGuildConfig>(
        id: GuildId,
        conn: &mut RedisPool,
    ) -> std::result::Result<T, redis::RedisError> {
        Ok(Self::fetch::<T>(id, conn).await?.unwrap_or_else(|| T::new()))
    }

    pub fn set<T: ::protobuf::Message + CachedGuildConfig>(id: GuildId, value: T) -> redis::Cmd {
        let key = CacheKey(CachePrefix::GuildConfigs, id.0);
        redis::Cmd::hset(key, vec![T::SUBKEY], Compressed(Protobuf(value)))
    }
}

pub struct CachedMessage {
    proto: Protobuf<CachedMessageProto>,
}

impl CachedMessage {
    pub fn new(message: impl MessageLike) -> Self {
        let mut msg = CachedMessageProto::new();
        msg.set_id(message.id().0);
        msg.set_channel_id(message.channel_id().0);
        msg.set_content(message.content().to_owned());
        if let Some(guild_id) = message.guild_id() {
            msg.set_guild_id(guild_id.0)
        }

        let user = msg.mut_author();
        let author = message.author();
        user.set_id(author.id().0);
        user.set_username(author.name().to_owned());
        user.set_discriminator(author.discriminator() as u32);
        user.set_bot(author.bot());
        if let Some(avatar) = author.avatar_hash() {
            user.set_avatar(avatar.to_owned());
        }

        Self {
            proto: Protobuf(msg),
        }
    }

    pub async fn fetch<C: ConnectionLike>(
        channel_id: ChannelId,
        message_id: MessageId,
        conn: &mut C,
    ) -> Result<Option<CachedMessageProto>> {
        let key = CacheKey(CachePrefix::Messages, (channel_id.0, message_id.0));
        let proto: Option<Protobuf<CachedMessageProto>> =
            redis::Cmd::get(key).query_async(conn).await?;
        Ok(proto.map(|proto| proto.0))
    }

    pub fn flush(self) -> redis::Pipeline {
        let channel_id = self.proto.0.get_channel_id();
        let id = self.proto.0.get_id();
        let key = CacheKey(CachePrefix::Messages, (channel_id, id));
        let mut pipeline = redis::pipe();
        // Keep 1 day's worth of messages cached.
        pipeline.atomic().set(key, self.proto).expire(key, 86400);
        pipeline
    }

    pub fn delete(channel_id: ChannelId, id: MessageId) -> redis::Cmd {
        Self::bulk_delete(channel_id, vec![id])
    }

    pub fn bulk_delete(
        channel_id: ChannelId,
        ids: impl IntoIterator<Item = MessageId>,
    ) -> redis::Cmd {
        let keys: Vec<CacheKey<(u64, u64)>> = ids
            .into_iter()
            .map(|id| CacheKey(CachePrefix::Messages, (channel_id.0, id.0)))
            .collect();
        redis::Cmd::del(keys)
    }
}
