import asyncio
import discord
from discord.ext import commands, tasks
from hourai.bot import cogs
from hourai.db import models
from hourai.utils import iterable


class BanLogging(cogs.BaseCog):
    """ Cog for logging guild bans. """

    def __init__(self, bot):
        super().__init__()
        self.bot = bot
        self.reload_bans.start()

    def cog_unload(self):
        self.reload_bans.cancel()

    @tasks.loop(seconds=180)
    async def reload_bans(self):
        for chunk in iterable.chunked(self.bot.guilds, chunk_size=5):
            await asyncio.gather(*[
                self.save_bans(guild) for guild in chunk])

    @reload_bans.before_loop
    async def before_reload_bans(self):
        await self.bot.wait_until_ready()

    @commands.Cog.listener()
    async def on_guild_join(self, guild):
        await self.save_bans(guild)

    @commands.Cog.listener()
    async def on_guild_remove(self, guild):
        await self.bot.storage.bans.clear_guild(guild.id)

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        await self.bot.wait_until_ready()
        if not guild.me.guild_permissions.ban_members:
            return
        try:
            ban_info = await guild.fetch_ban(user)
            await self.bot.storage.bans.save_ban(guild, ban_info)
        except discord.Forbidden:
            pass

    @commands.Cog.listener()
    async def on_member_unban(self, guild, user):
        await self.bot.storage.bans.clear_ban(guild, user)

    async def save_bans(self, guild):
        try:
            await self.bot.storage.bans.save_bans(guild)
        except Exception:
            log.exception(
                f"Exception while reloading bans for guild {guild.id}:")
