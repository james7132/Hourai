from hourai.bot import cogs
from hourai.bot import CounterKeys
from discord.ext import commands


class Counters(cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot

    @commands.Cog.listener()
    async def on_message(self, message):
        key = CounterKeys.MESSAGES_RECIEVED
        if message.guild is not None:
            self.bot.guild_counters[message.guild.id][key] += 1
