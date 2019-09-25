import aioredis
import asyncio
import collections
import logging

log = logging.getLogger(__name__)

BanEntry = collections.namedtuple('BanEntry', 'guild_id user_id reason')

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

        if len(bans) <= 0:
            return

        mapping = {(guild.id, ban.user.id): ban.reason or ''
                   for ban in bans}
        await self.storage.bans.set_all(mapping)

    async def save_ban(self, guild_id, user_id, reason):
        await self.storage.bans.set((guild_id, user_id), reason)

    async def get_bans(self, user_id, guild_ids):
        """Gets the ban information for a given user for a set of guilds.
        Returns: an async generator of BanEntry objects.
        """
        try:
            keys = ((guild_id, user_id) for guild_id in guild_ids)
            results = await self.storage.bans.get_all(keys)
            return [BanEntry(guild_id=key[0], user_id=key[1],
                             reason=value if value != '' else None)
                    for key, value in results.items() if value is not None]
        except aioredis.MultiExecError:
            log.exception('Failure in fetching bans:')
            raise

    async def clear_ban(self, guild, user):
        """Clears a ban from the storage.
        Params:
            guild_id [int] - The guild ID.
            user_id  [int] - The user ID.
        """
        await self.storage.bans.clear((guild.id, user.id))
