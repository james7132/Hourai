import dbl
import logging
from discord.ext import commands
from hourai.bot import cogs


class TopGG(cogs.BaseCog):
    """Handles interactions with the top.gg API"""

    def __init__(self, bot):
        self.bot = bot
        self.token = bot.config.third_party.top_gg_token
        self.dblpy = None
        if self.token:
            self.dblpy = dbl.DBLClient(self.bot, self.token, autopost=True)

    @commands.Cog.listener()
    async def on_guild_post(self):
        self.bot.logger.info(f'Server count posted successfully.')
