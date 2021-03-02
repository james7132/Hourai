use async_trait::async_trait;
use crate::error::Result;
use byteorder::{BigEndian, ByteOrder};
use crate::proto::auto_config::*;
use crate::proto::guild_configs::*;
use mobc_redis::redis::aio::ConnectionLike;
use mobc_redis::redis::{RedisWrite, ToRedisArgs};
use mobc_redis::redis;
use num_derive::FromPrimitive;
use num_traits::FromPrimitive;
use std::io::prelude::*;
use twilight_model::id::*;

/// The single byte compression mode header for values stored in Redis.
#[repr(u8)]
#[derive(FromPrimitive)]
enum CompressionMode {
    /// Uncompressed. The value is entirely uncompressed and can be used as is.
    Uncompressed = 0,
    /// Compressed with zlib. Default compression level: 6.
    Zlib = 1,
}

/// The single byte key prefix for all keys stored in Redis.
#[repr(u8)]
#[derive(Copy, Clone)]
enum CachePrefix {
    /// Protobuf configs for per server configuration. Stored in the form of hashes with individual
    /// configs as hash values, keyed by the corresponding CachedGuildConfig subkey.
    GuildConfigs = 1_u8,
}

/// A prefixed key schema for 64-bit integer keys. Implements ToRedisArgs, so its generically
/// usable as an argument to direct Redis calls.
#[derive(Copy, Clone)]
struct CacheKey8(CachePrefix, u64);

impl ToRedisArgs for CacheKey8 {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [self.0 as u8; 9];
        BigEndian::write_u64(&mut key_enc[1..9], self.1);
        out.write_arg(&key_enc[..]);
    }
}

struct Id8(u64);

impl ToRedisArgs for Id8 {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [8 as u8; 8];
        BigEndian::write_u64(&mut key_enc[0..8], self.0);
        out.write_arg(&key_enc[..]);
    }
}

fn compress_payload(payload: &[u8]) -> Result<Vec<u8>> {
    let mut encoder = flate2::write::ZlibEncoder::new(Vec::new(), flate2::Compression::new(6));
    encoder.write_all(&payload)?;
    let mut output = encoder.finish()?;
    let compression_mode = if output.len() < payload.len() {
        CompressionMode::Zlib
    } else {
        output = payload.to_vec();
        CompressionMode::Uncompressed
    };
    output.insert(0, compression_mode as u8);
    return Ok(output);
}

fn decompress_payload(payload: &[u8]) -> Result<Vec<u8>> {
    if payload.len() <= 0 {
        return Ok(payload.to_vec());
    }
    let data = &payload[1..];
    return match CompressionMode::from_u8(payload[0]) {
        Some(CompressionMode::Uncompressed) => Ok(data.to_vec()),
        Some(CompressionMode::Zlib) => {
            let mut output: Vec<u8> = Vec::new();
            flate2::read::ZlibDecoder::new(data).read_to_end(&mut output)?;
            Ok(output)
        }
        // Default to returning the original payload if no match for the header is found
        None => Ok(payload.to_vec()),
    };
}

#[async_trait]
pub trait Cacheable: Sized {
    type Key;
    async fn get<I, C>(connection: &mut C, key: I) -> Result<Option<Self>>
    where
        I: Into<Self::Key> + Send,
        C: ConnectionLike + Send;
    async fn set<I, C>(connection: &mut C, key: I, value: &Self) -> Result<()>
    where
        I: Into<Self::Key> + Send,
        C: ConnectionLike + Send;
}

#[async_trait]
impl<T: protobuf::Message + CachedGuildConfig + Send> Cacheable for T {
    type Key = GuildId;

    async fn get<I, C>(connection: &mut C, key: I) -> Result<Option<Self>>
    where
        I: Into<GuildId> + Send,
        C: ConnectionLike + Send,
    {
        let key = CacheKey8(CachePrefix::GuildConfigs, key.into().0);
        let response: Option<Vec<u8>> = redis::Cmd::hget(key, Self::SUBKEY)
            .query_async(connection)
            .await?;
        let proto = if let Some(payload) = response {
            let decomp = decompress_payload(&payload[..])?;
            Self::parse_from_bytes(&decomp[..])?
        } else {
            // If nothing has been found, return the default value for the type.
            Self::new()
        };
        return Ok(Some(proto));
    }

    async fn set<I, C>(connection: &mut C, key: I, value: &Self) -> Result<()>
    where
        I: Into<GuildId> + Send,
        C: ConnectionLike + Send,
    {
        let mut proto_enc: Vec<u8> = Vec::new();
        value.write_to_vec(&mut proto_enc)?;
        let compressed = compress_payload(&proto_enc[..])?;
        let key = CacheKey8(CachePrefix::GuildConfigs, key.into().0);
        redis::Cmd::hset(key, Self::SUBKEY, compressed)
            .query_async(connection)
            .await?;
        return Ok(());
    }
}

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
