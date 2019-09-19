import asyncio
import struct
import collections
from hourai.db.storage import StoragePrefix

BanEntry = collections.namedtuple('BanEntry', ['guild_id', 'user_id', 'reason'])
PACK_FORMAT = ">Q"

PREFIX = bytes([StoragePrefix.BANS.value])

def _get_key(guild_id, user_id):
    # User ID first to make it easier to look up
    key = b'%b%b%b' % (PREFIX, struct.pack(PACK_FORMAT, user_id),
                       struct.pack(PACK_FORMAT, guild_id))
    assert len(key) == 17
    return key

def _parse_key(key):
    """Return format: user_id, guild_id"""
    assert len(key) == 17
    return (struct.unpack_from(PACK_FORMAT, key, 1),
            struct.unpack_from(PACK_FORMAT, key, 9))

def weave(*gens):
    for vals in zip(*gens):
        for val in vals:
            yield val

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
        bans = await guild.bans()

        tr = self.redis.multi_exec()
        for ban in bans:
            tr.set(key=_get_key(guild.id, ban.user.id),
                   value=ban.reason or '',
                   expire=self.timeout)
        await tr.execute()

    async def save_ban(self, guild, ban):
        await self.redis.set(key=_get_key(guild.id, ban.user.id),
                             value=ban.reason or '',
                             expire=self.timeout)

    async def get_bans(self, user_id):
        """Gets the ban information for a given user.
        Returns: an async generator of BanEntry objects.
        """
        prefix = 'ban:{}'.format(user_id)
        cur = prefix
        match = 'ban:{}:*'.format(user_id)
        seen_keys = set()
        while cur and cur.startswith(prefix):
            cur, keys = await self.redis.scan(cur, match=match)
            values = await self.redis.mget(*keys)
            for key, reason in zip(keys, reason):
                if key in seen_keys or len(reason) <= 0:
                    reason = None
                user_id, guild_id = _parse_key(key)
                yield BanEntry(guild_id=guild_id, user_id=user_id,
                               reason=reason)
                seen_keys.add(key)
