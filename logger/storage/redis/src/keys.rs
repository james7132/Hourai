use byteorder::{BigEndian, ByteOrder};
use hourai::models::id::*;
use redis::{RedisWrite, ToRedisArgs};

/// The single byte key prefix for all keys stored in Redis.
#[derive(Copy, Clone)]
pub enum CacheKey {
    /// Protobuf configs for per server configuration. Stored in the form of hashes with individual
    /// configs as hash values, keyed by the corresponding CachedGuildConfig subkey.
    GuildConfigs(/* Guild ID */ u64),
    /// Redis sets of per-server user IDs of online users.
    OnlineStatus(/* Guild ID */ u64),
    /// Messages cached.
    Messages(/* Channel ID */ u64, /* Message ID */ u64),
    /// Cached guild data.
    Guild(/* Guild ID */ u64),
    /// Cached voice state data.
    VoiceState(/* Guild ID */ u64),
}

impl CacheKey {
    pub fn prefix(&self) -> u8 {
        match self {
            Self::GuildConfigs(_) => 1_u8,
            Self::OnlineStatus(_) => 2_u8,
            Self::Messages(_, _) => 3_u8,
            Self::Guild(_) => 4_u8,
            Self::VoiceState(_) => 5_u8,
        }
    }
}

impl ToRedisArgs for CacheKey {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        match self {
            Self::GuildConfigs(id) => PrefixedKey(self.prefix(), *id).write_redis_args(out),
            Self::OnlineStatus(id) => PrefixedKey(self.prefix(), *id).write_redis_args(out),
            Self::Messages(ch_id, msg_id) => {
                PrefixedKey(self.prefix(), (*ch_id, *msg_id)).write_redis_args(out)
            }
            Self::Guild(id) => PrefixedKey(self.prefix(), *id).write_redis_args(out),
            Self::VoiceState(id) => PrefixedKey(self.prefix(), *id).write_redis_args(out),
        }
    }
}

/// The single byte key prefix for guild keys stored in Redis.
#[derive(Copy, Clone)]
pub enum GuildKey {
    /// Guild level data. No secondary key. Maps to a CachedGuildProto.
    Guild,
    /// Role level data. Requires role id as secondary key. Maps to CachedRoleProto.
    Role(RoleId),
    /// Channels. Requires channel id as secondary key. Maps to CachedGuildChannelProto.
    Channel(ChannelId),
}

impl GuildKey {
    pub fn prefix(&self) -> u8 {
        match self {
            Self::Guild => 1_u8,
            Self::Role(_) => 2_u8,
            Self::Channel(_) => 3_u8,
        }
    }
}

impl ToRedisArgs for GuildKey {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        match self {
            Self::Guild => PrefixedKey(self.prefix(), ()).write_redis_args(out),
            Self::Role(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
            Self::Channel(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
        }
    }
}

impl From<GuildId> for GuildKey {
    fn from(_: GuildId) -> Self {
        Self::Guild
    }
}

impl From<RoleId> for GuildKey {
    fn from(value: RoleId) -> Self {
        Self::Role(value)
    }
}

impl From<ChannelId> for GuildKey {
    fn from(value: ChannelId) -> Self {
        Self::Channel(value)
    }
}

/// A prefixed key schema for 64-bit integer keys. Implements ToRedisArgs, so its generically
/// usable as an argument to direct Redis calls.
#[derive(Copy, Clone)]
pub struct PrefixedKey<T>(u8, T);

impl ToRedisArgs for PrefixedKey<()> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let key_enc = [self.0.clone().into(); 1];
        out.write_arg(&key_enc[..]);
    }
}

impl ToRedisArgs for PrefixedKey<u64> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        let mut key_enc = [self.0.clone().into(); 9];
        BigEndian::write_u64(&mut key_enc[1..9], self.1);
        out.write_arg(&key_enc[..]);
    }
}

impl ToRedisArgs for PrefixedKey<(u64, u64)> {
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
