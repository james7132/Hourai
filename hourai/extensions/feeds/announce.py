import asyncio
import discord
import random
from hourai import cogs
from hourai.db import proto
from discord.ext import commands

class Announce(cogs.BaseCog):

    def __init__(self, bot):
        self.bot = bot

    async def get_announce_config(self, guild):
        config = await self.bot.storage.announce_configs.get(guild.id)
        return config or proto.AnnoucementConfig()

    @commands.Cog.listener()
    async def on_member_join(self, member):
        announce_config = self.get_announce_config(member.guild)
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
        announce_config = self.get_announce_config(member.guild)
        if not announce_config.HasField('leaves'):
            return
        if len(announce_config.leaves.messages) > 0:
            choices = list(announce_config.leaves.messages)
        else:
            choices = [f'**{member.mention}** has left the server.']
        await self.__make_announcement(member.guild, announce_config.leaves,
                                       choices)

    @commands.Cog.listener()
    async def on_member_ban(self, member):
        announce_config = self.get_announce_config(member.guild)
        if not announce_config.HasField('bans'):
            return
        if len(announce_config.bans.messages) > 0:
            choices = list(announce_config.bans.messages)
        else:
            choices = [f'**{member.mention** has been banned.']
        await self.__make_announcement(member.guild, announce_config.leaves,
                                       choices)

    @commands.Cog.listener()
    async def on_voice_state_update(self, member, before, after):
        announce_config = self.get_announce_config(member.guild)
        if not announce_config.HasField('voice'):
            return
        assert not (before.channel is None and after.channel is None)
        if before.channel == after.channel:
            return
        elif before.channel is None:
            choices = [f'**{member.display_name}** joined **{before.name}**.']
        elif after.channel is None:
            choices = [f'**{member.display_name}** left **{after.name}**.']
        else:
            choices = [f'**{member.display_name}** moved to **{after.name}**'
                       f' from **{before.name**.']
        await self.__make_announcement(member.guild, announce_config.voice,
                                       choices)

    async def __make_announcement(self, guild, config, choices):
        assert len(choices) > 0
        channels = [guild.get_channel(ch_id) for ch_id in config.channel_ids]
        channels = [ch for ch in channels
                    if isinstance(ch, discord.TextChannel)]
        tasks = []
        for channel in channels:
            content = random.choice(config.messages)
            tasks.append(channel.send(content))
        await asyncio.gather(*tasks)
