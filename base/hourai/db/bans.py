import aioredis
import asyncio
import collections
import logging
import coders
import time
from . import proto
from hourai.utils import iterable

log = logging.getLogger(__name__)

BanEntry = collections.namedtuple('BanEntry', 'guild_id user_id avatar reason')

GUILD_BAN_PREFIX = 0
USER_BAN_PREFIX = 1
MAX_CHUNK_SIZE = 1024


def _get_guild_size(guild):
    if not guild.chunked:
        return guild.member_count
    return len([m for m in guild.members if not m.bot])


async def redis_transaction(redis, txn_func):
    try:
        tr = redis.multi_exec()
        futs = list(txn_func(tr))
        results = await tr.execute()
        assert results == (await asyncio.gather(*futs))
        return results
    except aioredis.MultiExecError:
        log.exception('Failure in Redis Transaction:')
        raise

class BanStorage:
    """An interface for access store all of the bans seen by the bot."""

    def __init__(self, storage, prefix, timeout=300):
        self.storage = storage
        self.timeout = timeout

        guild_prefix = bytes([prefix, GUILD_BAN_PREFIX])
        user_prefix = bytes([prefix, USER_BAN_PREFIX])

        self._guild_key_coder = coders.IntCoder().prefixed(guild_prefix)
        self._user_key_coder = coders.IntCoder().prefixed(user_prefix)

        self._guild_value_coder = coders.ProtobufCoder(proto.BanInfo) \
                                       .compressed()
        self._id_coder = coders.IntCoder()

    @property
    def redis(self):
        return self.storage.redis

    async def save_bans(self, guild):
        """Atomically saves all of the bans for a given guild to the backng
        store.
        """
        if not guild.me.guild_permissions.ban_members:
            return
        bans = await guild.bans()

        if len(bans) <= 0:
            return

        guild_key = self._guild_key_coder.encode(guild.id)
        ban_protos = (self.__encode_ban(guild, ban) for ban in bans)

        def transaction(tr):
            for chunk in iterable.chunked(ban_protos, MAX_CHUNK_SIZE):
                yield tr.hmset_dict(guild_key, dict(chunk))
            for ban in bans:
                user_key = self._guild_key_coder.encode(ban.user.id)
                yield tr.sadd(user_key, guild_key)
                yield tr.expire(user_key, self.timeout)
            yield tr.expire(guild_key, self.timeout)
        await redis_transaction(self.redis, transaction)

    async def save_ban(self, guild, ban):
        guild_key = self._guild_key_coder.encode(guild.id)
        user_key = self._user_key_coder.encode(ban.user.id)
        user_id_enc, guild_value = self.__encode_ban(guild, ban)

        def transaction(tr):
            yield tr.hset(guild_key, user_id_enc, guild_value)
            yield tr.sadd(user_key, guild_key)
            yield tr.expire(guild_key, self.timeout)
            yield tr.expire(user_key, self.timeout)
        await redis_transaction(self.redis, transaction)

    async def get_guild_bans(self, guild_id):
        guild_key = self._guild_key_coder.encode(guild_id)
        bans_enc = await self.redis.hgetall(guild_key)

        if bans_enc is None:
            return []

        return [self._guild_value_coder.decode(proto_enc)
                for _, proto_enc in bans_enc.items()]

    async def get_user_bans(self, user_id):
        user_key = self._user_key_coder.encode(user_id)
        guild_keys = await self.redis.smembers(user_key)

        if guild_keys is None or len(guild_keys) <= 0:
            return []

        user_id_enc = self._id_coder.encode(user_id)
        def transacation(tr):
            for key in guild_keys:
                yield tr.hget(key, user_id_enc)
        results = await redis_transaction(self.redis, transaction)
        return [self._guild_value_coder.decode(proto_enc)
                for proto_enc in results if proto_enc is not None]

    async def clear_guild(self, guild_id):
        guild_key = self._guild_key_coder.encode(guild_id)
        bans_enc = await self.redis.hgetall(guild_key)

        if bans_enc is None:
            return

        def transaction(tr):
            yield tr.delete(guild_key)
            for id_enc, proto_enc in bans_enc.items():
                user_id = self._id_coder.decode(id_enc)
                user_key = self._user_key_coder.encode(user_id)
                yield tr.srem(user_key, guild_key)
        await redis_transaction(self.redis, transaction)

    async def clear_ban(self, guild, user):
        guild_key = self._guild_key_coder.encode(guild.id)
        user_key = self._user_key_coder.encode(user.id)
        user_id_enc = self._id_coder.encode(user.id)

        def transaction(tr):
            yield tr.hdel(guild_key, user_id_enc),
            yield tr.srem(user_key, guild_key)
        await redis_transaction(self.redis, transaction)

    def __encode_ban(self, guild, ban):
        ban_proto = proto.BanInfo()
        ban_proto.guild_id = guild.id
        ban_proto.guild_size = _get_guild_size(guild)
        ban_proto.user_id = ban.user.id
        if ban.user.avatar is not None:
            ban_proto.avatar = ban.user.avatar
        if ban.reason is not None:
            ban_proto.reason = ban.reason

        id_enc = self._id_coder.encode(ban.user.id)
        proto_enc = self._guild_value_coder.encode(ban_proto)
        return (id_enc, proto_enc)
