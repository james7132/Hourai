import asyncio
import discord
import random
from hourai.bot import cogs
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
        conf = await ctx.guild_proxy.config.edit('announce')
        result = self.__toggle_channel(ctx, conf.joins)
        await ctx.guild_proxy.config.set('announce', conf)
        suffix = 'enabled' if result else 'disabled'
        await ctx.send(f":thumbsup: Join messages {suffix}")

    @announce.command(name='leave')
    async def announce_leave(self, ctx):
        conf = await ctx.guild_proxy.config.edit('announce')
        result = self.__toggle_channel(ctx, conf.leaves)
        await ctx.guild_proxy.config.set('announce', conf)
        suffix = 'enabled' if result else 'disabled'
        await ctx.send(f":thumbsup: Leave  messages {suffix}")

    @announce.command(name='ban')
    async def announce_ban(self, ctx):
        conf = await ctx.guild_proxy.config.edit('announce')
        result = self.__toggle_channel(ctx, conf.bans)
        await ctx.guild_proxy.config.set('announce', conf)
        suffix = 'enabled' if result else 'disabled'
        await ctx.send(f":thumbsup: Ban messages {suffix}")

    def __toggle_channel(self, ctx, config):
        if ctx.channel.id in config.channel_ids:
            config.channel_ids.remove(ctx.channel.id)
            return False
        config.channel_ids.append(ctx.channel.id)
        return True

    @commands.Cog.listener()
    async def on_member_join(self, member):
        proxy = self.bot.get_guild_proxy(member.guild)
        announce_config = await proxy.config.get('announce')
        if not announce_config.HasField('joins'):
            return
        if len(announce_config.joins.messages) > 0:
            choices = list(announce_config.joins.messages)
        else:
            choices = [f'**{member.mention}** has joined the server.']
        await self.__make_announcement(member.guild, announce_config.joins,
                                       choices)

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        proxy = self.bot.get_guild_proxy(member.guild)
        announce_config = await proxy.config.get('announce')
        if not announce_config.HasField('leaves'):
            return
        if len(announce_config.leaves.messages) > 0:
            choices = list(announce_config.leaves.messages)
        else:
            choices = [f'**{member.name}** has left the server.']
        await self.__make_announcement(member.guild, announce_config.leaves,
                                       choices)

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        proxy = self.bot.get_guild_proxy(guild)
        announce_config = await proxy.config.get('announce')
        if not announce_config.HasField('bans'):
            return
        if len(announce_config.bans.messages) > 0:
            choices = list(announce_config.bans.messages)
        else:
            choices = [f'**{user.name}** has been banned.']
        await self.__make_announcement(guild, announce_config.bans, choices)

    @commands.Cog.listener()
    async def on_voice_state_update(self, member, before, after):
        proxy = self.bot.get_guild_proxy(member.guild)
        announce_config = await proxy.config.get('announce')
        if not announce_config.HasField('voice'):
            return
        assert not (before.channel is None and after.channel is None)
        if before.channel == after.channel:
            return
        elif before.channel is None:
            choices = [f'**{member.display_name}** joined **{after.channel.name}**.']
        elif after.channel is None:
            choices = [f'**{member.display_name}** left **{before.channel.name}**.']
        else:
            choices = [f'**{member.display_name}** moved to **{after.channel.name}**'
                       f' from **{before.channel.name}**.']
        await self.__make_announcement(member.guild, announce_config.voice,
                                       choices)

    async def __make_announcement(self, guild, config, choices):
        assert len(choices) > 0
        channels = [guild.get_channel(ch_id) for ch_id in config.channel_ids]
        channels = [ch for ch in channels
                    if isinstance(ch, discord.TextChannel)]
        tasks = []
        for channel in channels:
            content = random.choice(choices)
            tasks.append(channel.send(content))
        try:
            await asyncio.gather(*tasks)
        except discord.errors.Forbidden:
            pass


def setup(bot):
    bot.add_cog(Announce(bot))
