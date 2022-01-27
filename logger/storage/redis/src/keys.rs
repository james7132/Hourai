use crate::GuildResource;
use byteorder::{BigEndian, ByteOrder};
use hourai::models::{
    channel::GuildChannel,
    guild::{Guild, Role},
    id::{marker::*, Id as TwilightId},
};
use redis::{ErrorKind, FromRedisValue, RedisError, RedisWrite, ToRedisArgs};

/// The single byte key prefix for all keys stored in Redis.
#[derive(Clone)]
pub enum CacheKey {
    /// Protobuf configs for per server configuration. Stored in the form of hashes with individual
    /// configs as hash values, keyed by the corresponding CachedGuildConfig subkey.
    GuildConfigs(TwilightId<GuildMarker>),
    /// Redis sets of per-server user IDs of online users.
    OnlineStatus(TwilightId<GuildMarker>),
    /// Messages cached.
    Messages(TwilightId<ChannelMarker>, TwilightId<MessageMarker>),
    /// Cached guild data.
    Guild(TwilightId<GuildMarker>),
    /// Cached voice state data.
    VoiceState(TwilightId<GuildMarker>),
    /// Resume State
    ResumeState(/* Name */ String),
    /// The stored music queues for each server. Used to restore the music state after a restart.
    MusicQueue(TwilightId<GuildMarker>),
}

impl CacheKey {
    pub fn prefix(&self) -> u8 {
        match self {
            Self::GuildConfigs(_) => 1_u8,
            Self::OnlineStatus(_) => 2_u8,
            Self::Messages(_, _) => 3_u8,
            Self::Guild(_) => 4_u8,
            Self::VoiceState(_) => 5_u8,
            Self::ResumeState(_) => 6_u8,
            Self::MusicQueue(_) => 7_u8,
        }
    }
}

impl ToRedisArgs for CacheKey {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        match self {
            Self::GuildConfigs(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
            Self::OnlineStatus(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
            Self::Messages(ch_id, msg_id) => {
                PrefixedKey(self.prefix(), (ch_id.get(), msg_id.get())).write_redis_args(out)
            }
            Self::Guild(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
            Self::VoiceState(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
            Self::ResumeState(key) => {
                PrefixedKey(self.prefix(), key.as_str()).write_redis_args(out)
            }
            Self::MusicQueue(id) => PrefixedKey(self.prefix(), id.get()).write_redis_args(out),
        }
    }
}

/// The single byte key prefix for guild keys stored in Redis.
#[derive(Copy, Clone, Debug, Eq, Hash, PartialEq)]
pub enum GuildKey {
    /// Guild level data. No secondary key. Maps to a CachedGuildProto.
    Guild,
    /// Role level data. Requires role id as secondary key. Maps to CachedRoleProto.
    Role(TwilightId<RoleMarker>),
    /// Channels. Requires channel id as secondary key. Maps to CachedGuildChannelProto.
    Channel(TwilightId<ChannelMarker>),
}

impl GuildKey {
    pub fn prefix(&self) -> u8 {
        match self {
            Self::Guild => Guild::PREFIX,
            Self::Role(_) => Role::PREFIX,
            Self::Channel(_) => GuildChannel::PREFIX,
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

impl FromRedisValue for GuildKey {
    fn from_redis_value(value: &redis::Value) -> redis::RedisResult<Self> {
        if let redis::Value::Data(data) = value {
            match (data.get(0), data.len()) {
                (Some(&Guild::PREFIX), _) => Ok(Self::Guild),
                (Some(&Role::PREFIX), len) if len >= 9 => {
                    let id = BigEndian::read_u64(&data[1..9]);
                    Ok(Self::Role(TwilightId::new(id)))
                }
                (Some(&GuildChannel::PREFIX), len) if len >= 9 => {
                    let id = BigEndian::read_u64(&data[1..9]);
                    Ok(Self::Channel(TwilightId::new(id)))
                }
                _ => Err(RedisError::from((
                    ErrorKind::ResponseError,
                    "Invalid GuildKey",
                ))),
            }
        } else {
            Err(RedisError::from((
                ErrorKind::ResponseError,
                "Guild key not from data",
            )))
        }
    }
}

impl From<TwilightId<GuildMarker>> for GuildKey {
    fn from(_: TwilightId<GuildMarker>) -> Self {
        Self::Guild
    }
}

impl From<TwilightId<RoleMarker>> for GuildKey {
    fn from(value: TwilightId<RoleMarker>) -> Self {
        Self::Role(value)
    }
}

impl From<TwilightId<ChannelMarker>> for GuildKey {
    fn from(value: TwilightId<ChannelMarker>) -> Self {
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

impl ToRedisArgs for PrefixedKey<&str> {
    fn write_redis_args<W: ?Sized>(&self, out: &mut W)
    where
        W: RedisWrite,
    {
        self.0.write_redis_args(out);
        self.1.write_redis_args(out);
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
