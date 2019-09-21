import aioredis
import asyncio
import struct
import collections
import logging
from aioredis.util import _NOTSET
from hourai.db.storage import StoragePrefix

log = logging.getLogger(__name__)

BanEntry = collections.namedtuple('BanEntry', ['guild_id', 'user_id', 'reason'])
PACK_FORMAT = ">Q"

PREFIX = bytes([StoragePrefix.BANS.value])

def _pack(id):
    return struct.pack(PACK_FORMAT, id)

def _get_key(guild_id):
    # User ID first to make it easier to look up
    key = bytearray(9)
    key[0] = PREFIX[0]
    struct.pack_into(PACK_FORMAT, key, 1, guild_id)
    return key

def _parse_key(key):
    """Return format: guild_id"""
    assert len(key) == 9
    return struct.unpack_from(PACK_FORMAT, key, 1)

class BanReasonCoder:

    def serialize(self, reason):
        return reason.encode('utf-8') if reason is not None else b''

    def deserialize(self, buf):
        return buf.decode('utf-8') if buf!= b'' else None

class BanStorage:
    """An interface for access store all of the bans seen by the bot.

    This information is transitive and is never written to disc.
    Implemented as a Redis mapping:
        - Key:      "ban:{user_id}:{guild_id}"
        - Value:    Ban Reason. Empty string if None.
        - Timeout:  Constant provided at initialization.
    """

    def __init__(self, bot, timeout=300):
        self.storage = bot.storage
        self.timeout = timeout
        self.coder = BanReasonCoder()

    @property
    def redis(self):
        return self.storage.redis

    async def save_all_bans(self, bot):
        """Saves all bans for every guild a bot is in to the store."""
        await asyncio.gather(*[self.save_bans(guild) for guild in bot.guilds])

    async def save_bans(self, guild):
        """Atomically saves all of the bans for a given guild to the backng
        store.
        """
        if not guild.me.guild_permissions.ban_members:
            return
        key = _get_key(guild.id)
        bans = await guild.bans()

        if len(bans) <= 0:
            return

        tr = self.redis.multi_exec()
        tr.delete(key)
        tr.hmset_dict(_get_key(guild.id),
                      {_pack(ban.user.id): self.coder.serialize(ban.reason)
                       for ban in bans})
        tr.expire(key, self.timeout)
        await tr.execute()

    async def save_ban(self, guild_id, user_id, reason):
        key = _get_key(guild.id)
        tr = self.redis.multi_exec()
        tr.hset(key, _pack(user_id), self.coder.serialize(reason))
        tr.expire(key)
        await tr.execute()

    async def get_bans(self, user_id, guild_ids):
        """Gets the ban information for a given user for a set of guilds.
        Returns: an async generator of BanEntry objects.
        """
        guild_keys = (_get_key(guild_id) for guild_id in guild_ids)
        user_key = _pack(user_id)

        try:
            tr = self.redis.multi_exec()
            tasks = [tr.hget(g_key, user_key) for g_key in guild_keys]
            reasons = await tr.execute()
            result = await asyncio.gather(*tasks)
            assert reasons == result

            return [BanEntry(guild_id=g_id, user_id=user_id,
                             reason=self.coder.deserialize(reason))
                    for g_id, reason in zip(guild_ids, reasons)
                    if reason is not None]
        except aioredis.MultiExecError:
            log.exception('Failure in fetching bans:')
            raise

    async def clear_ban(self, guild_id, user_id):
        """Clears a ban from the storage.
        Params:
            guild_id [int] - The guild ID.
            user_id  [int] - The user ID.
        """
        res = await self.redis.hdel(_get_key(guild_id), _pack(user_id))
        return res != 0
