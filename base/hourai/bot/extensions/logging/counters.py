from hourai.bot import cogs
from hourai.bot import CounterKeys
from discord.ext import commands


class Counters(cogs.BaseCog):

    # TODO(james7132): Flush counters to a timeseries database

    def __init__(self, bot):
        self.bot = bot

    @commands.Cog.listener()
    async def on_message(self, message):
        key = CounterKeys.MESSAGES_RECIEVED
        if message.guild is not None:
            self.bot.guild_counters[message.guild.id][key] += 1

    @commands.Cog.listener()
    async def on_raw_message_delete(self, payload):
        key = CounterKeys.MESSAGES_DELETED
        if payload.guild_id is not None:
            self.bot.guild_counters[payload.guild_id][key] += 1

    @commands.Cog.listener()
    async def on_raw_bulk_message_delete(self, payload):
        key = CounterKeys.MESSAGES_DELETED
        count = len(payload.message_ids)
        if payload.guild_id is not None:
            self.bot.guild_counters[payload.guild_id][key] += count

    @commands.Cog.listener()
    async def on_raw_message_edit(self, payload):
        key = CounterKeys.MESSAGES_EDITED
        # TODO(james7132): Update this when discord.py v1.3.x releases
        msg = payload.cached_message
        if msg is not None and msg.guild is not None:
            self.bot.guild_counters[msg.guild.id][key] += 1

    @commands.Cog.listener()
    async def on_member_join(self, member):
        if not member.pending:
            await self.on_join(member)

    @commands.Cog.listener()
    async def on_member_update(self, before, after):
        if before.pending and not after.pending:
            await self.on_join(after)

    async def on_join(self, member):
        self.__increment_guild_counter(member.guild,
                                       CounterKeys.MEMBERS_JOINED)

    @commands.Cog.listener()
    async def on_raw_member_remove(self, data):
        key = CounterKeys.MEMBERS_LEFT
        self.bot.guild_counters[int(data['guild_id'])][key] += 1

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        self.__increment_guild_counter(guild, CounterKeys.MEMBERS_BANNED)

    @commands.Cog.listener()
    async def on_member_unban(self, guild, user):
        self.__increment_guild_counter(guild, CounterKeys.MEMBERS_UNBANNED)

    @commands.Cog.listener()
    async def on_verify_accept(self, member):
        self.__increment_guild_counter(member.guild,
                                       CounterKeys.MEMBERS_VERIFIED)

    @commands.Cog.listener()
    async def on_verify_reject(self, member):
        self.__increment_guild_counter(member.guild,
                                       CounterKeys.MEMBERS_REJECTED)

    def __increment_guild_counter(self, guild, key, count=1):
        self.bot.guild_counters[guild.id][key] += count
