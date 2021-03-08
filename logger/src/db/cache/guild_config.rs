use crate::error::*;
pub use crate::proto::{
    auto_config::*,
    guild_configs::*,
    cache::*,
};
use super::{keys::{CacheKey, CachePrefix}, protobuf::Protobuf, compression::Compressed};
use twilight_model::id::GuildId;
use redis::aio::ConnectionLike;

pub trait CachedGuildConfig {
    const SUBKEY: u8;
}

macro_rules! guild_config {
    ($proto: ty, $key: expr) => {
        impl CachedGuildConfig for $proto {
            const SUBKEY: u8 = $key;
        }
    };
}

guild_config!(AutoConfig, 0_u8);
guild_config!(ModerationConfig, 1_u8);
guild_config!(LoggingConfig, 2_u8);
guild_config!(ValidationConfig, 3_u8);
guild_config!(MusicConfig, 4_u8);
guild_config!(AnnouncementConfig, 5_u8);
guild_config!(RoleConfig, 6_u8);

impl<T: protobuf::Message + CachedGuildConfig> Protobuf<T> {

    pub async fn get(id: GuildId, conn: &mut impl ConnectionLike) -> Result<T> {
        let key = CacheKey(CachePrefix::GuildConfigs, id.0);
        let response: Compressed<Self> = redis::Cmd::hget(key, T::SUBKEY)
            .query_async(conn)
            .await?;
        Ok(response.0.0)
    }

    pub fn set(id: GuildId, value: T) -> redis::Cmd {
        let key = CacheKey(CachePrefix::GuildConfigs, id.0);
        redis::Cmd::hset(key, T::SUBKEY, Compressed(Self(value)))
    }

}
