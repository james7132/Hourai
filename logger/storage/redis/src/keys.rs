use byteorder::{BigEndian, ByteOrder};
use hourai::models::id::*;
use redis::{RedisWrite, ToRedisArgs};

/// The single byte key prefix for all keys stored in Redis.
#[repr(u8)]
#[derive(Copy, Clone)]
pub enum CachePrefix {
    /// Protobuf configs for per server configuration. Stored in the form of hashes with individual
    /// configs as hash values, keyed by the corresponding CachedGuildConfig subkey.
    GuildConfigs = 1_u8,
    /// Redis sets of per-server user IDs of online users.
    OnlineStatus = 2_u8,
    /// Messages cached.
    Messages = 3_u8,
    /// Cached guild data.
    Guild = 4_u8,
    /// Cached voice state data.
    VoiceState = 5_u8,
}

impl CachePrefix {
    pub fn make_key<T>(self, data: T) -> PrefixedKey<Self, T> {
        PrefixedKey(self, data)
    }
}

impl From<CachePrefix> for u8 {
    fn from(value: CachePrefix) -> Self {
        value as u8
    }
}

/// The single byte key prefix for guild keys stored in Redis.
#[repr(u8)]
#[derive(Copy, Clone)]
pub enum GuildPrefix {
    /// Guild level data. No secondary key. Maps to a CachedGuildProto.
    Guild = 1_u8,
    /// Role level data. Requires role id as secondary key. Maps to CachedRoleProto.
    Role = 2_u8,
    /// Channels. Requires channel id as secondary key. Maps to CachedGuildChannelProto.
    Channel = 3_u8,
}

impl GuildPrefix {
    pub fn make_key<T>(self, data: T) -> PrefixedKey<Self, T> {
        PrefixedKey(self, data)
    }
}

impl From<GuildPrefix> for u8 {
    fn from(value: GuildPrefix) -> Self {
        value as u8
    }
}

impl From<GuildId> for GuildKey<()> {
    fn from(_: GuildId) -> Self {
        GuildPrefix::Guild.make_key(())
    }
}

impl From<RoleId> for GuildKey<u64> {
    fn from(value: RoleId) -> Self {
        GuildPrefix::Role.make_key(value.0)
    }
}

impl From<ChannelId> for GuildKey<u64> {
    fn from(value: ChannelId) -> Self {
        GuildPrefix::Channel.make_key(value.0)
    }
}

/// A prefixed key schema for 64-bit integer keys. Implements ToRedisArgs, so its generically
/// usable as an argument to direct Redis calls.
#[derive(Copy, Clone)]
pub struct PrefixedKey<P: Into<u8> + Clone, T>(pub P, pub T);
pub type CacheKey<T> = PrefixedKey<CachePrefix, T>;
pub type GuildKey<T> = PrefixedKey<GuildPrefix, T>;

impl<P: Into<u8> + Clone> ToRedisArgs for PrefixedKey<P, ()> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let key_enc = [self.0.clone().into(); 1];
        out.write_arg(&key_enc[..]);
    }
}

impl<P: Into<u8> + Clone> ToRedisArgs for PrefixedKey<P, u64> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [self.0.clone().into(); 9];
        BigEndian::write_u64(&mut key_enc[1..9], self.1);
        out.write_arg(&key_enc[..]);
    }
}

impl<P: Into<u8> + Clone> ToRedisArgs for PrefixedKey<P, (u64, u64)> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [self.0.clone().into(); 17];
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
