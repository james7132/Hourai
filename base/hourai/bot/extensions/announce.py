import asyncio
import discord
import random
from hourai.bot import cogs
from hourai.db import models
from discord.ext import commands


class Announce(cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
    @commands.has_permissions(manage_guild=True)
    @commands.bot_has_permissions(send_messages=True)
    async def announce(self, ctx):
        pass

    @announce.command(name='join')
    async def announce_join(self, ctx):
        conf = ctx.guild.config.announce
        result = self.__toggle_channel(ctx, conf.joins)
        await ctx.guild.flush_config()
        suffix = 'enabled' if result else 'disabled'
        await ctx.send(f":thumbsup: Join messages {suffix}")

    @announce.command(name='leave')
    async def announce_leave(self, ctx):
        conf = ctx.guild.config.announce
        result = self.__toggle_channel(ctx, conf.leaves)
        await ctx.guild.flush_config()
        suffix = 'enabled' if result else 'disabled'
        await ctx.send(f":thumbsup: Leave  messages {suffix}")

    @announce.command(name='ban')
    async def announce_ban(self, ctx):
        conf = ctx.guild.config.announce
        result = self.__toggle_channel(ctx, conf.bans)
        await ctx.guild.flush_config()
        suffix = 'enabled' if result else 'disabled'
        await ctx.send(f":thumbsup: Ban messages {suffix}")

    def __toggle_channel(self, ctx, config):
        if ctx.channel.id in config.channel_ids:
            config.channel_ids.remove(ctx.channel.id)
            return False
        config.channel_ids.append(ctx.channel.id)
        return True


def setup(bot):
    bot.add_cog(Announce(bot))
