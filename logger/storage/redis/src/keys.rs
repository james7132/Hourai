use byteorder::{BigEndian, ByteOrder};
use redis::{RedisWrite, ToRedisArgs};

/// The single byte key prefix for all keys stored in Redis.
#[repr(u8)]
#[derive(Copy, Clone)]
pub(super) enum CachePrefix {
    /// Protobuf configs for per server configuration. Stored in the form of hashes with individual
    /// configs as hash values, keyed by the corresponding CachedGuildConfig subkey.
    GuildConfigs = 1_u8,
    /// Redis sets of per-server user IDs of online users.
    OnlineStatus = 2_u8,
    /// Messages cached.
    Messages = 3_u8,
}

/// A prefixed key schema for 64-bit integer keys. Implements ToRedisArgs, so its generically
/// usable as an argument to direct Redis calls.
#[derive(Copy, Clone)]
pub(super) struct CacheKey<T>(pub CachePrefix, pub T);

impl ToRedisArgs for CacheKey<u64> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [self.0 as u8; 9];
        BigEndian::write_u64(&mut key_enc[1..9], self.1);
        out.write_arg(&key_enc[..]);
    }
}

impl ToRedisArgs for CacheKey<(u64, u64)> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [self.0 as u8; 17];
        BigEndian::write_u64(&mut key_enc[1..9], self.1 .0);
        BigEndian::write_u64(&mut key_enc[9..17], self.1 .1);
        out.write_arg(&key_enc[..]);
    }
}

#[derive(Copy, Clone)]
pub(super) struct Id<T>(pub T);

impl ToRedisArgs for Id<u64> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [0; 8];
        BigEndian::write_u64(&mut key_enc[0..8], self.0);
        out.write_arg(&key_enc[..]);
    }
}
