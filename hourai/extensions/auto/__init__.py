import asyncio
import copy
import discord
import re
from discord.ext import commands
from hourai import bot, utils, config
from hourai.db import proto

def meets_filter(val, filter_settings, default=False):
    if val is None:
        return default
    if filter_settings is None:
        return True
    meets_blacklist = any(re.search(pack, val)
                          for pack in filter_settings.blacklist)
    meets_whitelist = any(re.search(pack, val)
                          for pack in filter_settings.whitelist)
    return not meets_blacklist or meets_whitelist

def get_field(proto, field):
    return getattr(proto, field) if proto.HasField(field) else None

def is_valid_message_event(message, channel, evt):
    in_channel = channel is None or channel == message.channel
    is_filter_ok = meets_filter(message.clean_content,
                                get_field(evt, 'content_filter'))
    return in_channel and is_filter_ok

class Auto(bot.BaseCog):

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.Cog.listener()
    async def on_message(self, msg):
        await self.message_event(msg, proto.MessageEvent.MESSAGE_CREATES)

    @commands.Cog.listener()
    async def on_message_edit(self, _, msg):
        await self.message_event(msg, proto.MessageEvent.MESSAGE_EDITS)

    @commands.Cog.listener()
    async def on_member_join(self, member):
        await self.user_event(member.guild, member, 'on_join')

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        await self.user_event(member.guild, member, 'on_leave')

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        await self.user_event(guild, user, 'on_ban')

    async def user_event(self, guild, user, event_type):
        tasks = []
        async for channel, evt in self.get_events(guild, event_type):
            assert isinstance(evt, proto.UserChangeEvent)
            if not meets_filter(user.name, get_field(evt, 'username_filter')):
                continue
            actions = [copy.deepcopy(action) for action in evt.action]
            # TODO(james7132): Parameterize actions here
            tasks.append(self.execute_actions(actions))
        if len(tasks) > 0:
            await asyncio.gather(*tasks)

    async def message_event(self, msg, event_type):
        if msg.guild is None or msg.author == self.bot.user:
            return
        tasks = []
        delete = False
        async for channel, evt in self.get_events(msg.guild, 'on_message'):
            assert isinstance(evt, proto.MessageEvent)
            if ((evt.Type | event_type) == 0 or
                not is_valid_message_event(msg, channel, evt)):
                continue
            actions = [copy.deepcopy(action) for action in evt.action]
            # TODO(james7132): Parameterize actions here
            tasks.append(self.execute_actions(actions))
            delete = delete or evt.delete_message
        if len(tasks) > 0:
            await asyncio.gather(*tasks)
        if delete:
            await msg.delete()

    async def execute_actions(self, actions):
        raise NotImplementedError

    async def get_events(self, guild,  event_type):
        config = await self.get_auto_config(guild)
        if config is None:
            return
        for evt in getattr(config.guild_events, event_type):
            yield None, evt
        for key in config.channel_events:
            channel = discord.utils.get(guild.channels, name=key)
            if channel is None:
                continue
            for evt in getattr(config.channel_events[key], event_type):
                yield channel, evt

    async def get_auto_config(self, guild):
        if guild is None:
            return None
        return await self.bot.storage.auto_configs.get(guild.id)

def setup(bot):
    bot.add_cog(Auto(bot))
