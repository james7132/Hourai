import asyncio
import copy
import discord
import re
from .message_filter import MessageFilter
from discord.ext import commands
from hourai.bot import cogs
from hourai.db import proto


def meets_filter(val, filter_settings, default=True):
    if val is None:
        return False
    if filter_settings is None:
        return default
    meets_blacklist = any(re.search(pack, val)
                          for pack in filter_settings.blacklist)
    meets_whitelist = any(re.search(pack, val)
                          for pack in filter_settings.whitelist)
    if len(filter_settings.blacklist) > 0:
        return not meets_blacklist or meets_whitelist
    elif len(filter_settings.whitelist) > 0:
        return meets_whitelist
    return True


def get_field(proto, field, default=None):
    return getattr(proto, field) if proto.HasField(field) else default


def is_valid_message_event(message, channel, evt):
    in_channel = channel is None or channel == message.channel
    is_filter_ok = meets_filter(message.clean_content,
                                get_field(evt, 'content_filter'))
    return in_channel and is_filter_ok


def add_channel_id(channel, action):
    assert channel is not None
    action_type = action.WhichOneof('details')
    if action_type is None:
        return
    try:
        getattr(action, action_type).channel_id = channel.id
    except AttributeError:
        pass


def parameterize_actions(actions, user, guild, channel):
    for action in actions:
        action.guild_id = guild.id
        action.user_id = user.id
        if channel is not None:
            add_channel_id(channel, action)


class Auto(cogs.BaseCog):

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
        if member.pending:
            await self.user_event(member.guild, member, 'on_join')

    @commands.Cog.listener()
    async def on_member_update(self, before, after):
        if before.pending and not after.pending:
            await self.user_event(after.guild, after, 'on_join')

    @commands.Cog.listener()
    async def on_member_remove(self, member):
        await self.user_event(member.guild, member, 'on_leave')

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        await self.user_event(guild, user, 'on_ban')

    async def user_event(self, guild, user, event_type):
        if user.bot:
            return
        tasks = []
        async for channel, evt in self.get_events(guild, event_type):
            assert isinstance(evt, proto.UserChangeEvent)
            if not meets_filter(user.name, get_field(evt, 'username_filter')):
                continue
            actions = [copy.deepcopy(action) for action in evt.action]
            parameterize_actions(actions, user, guild, channel)
            tasks.append(self.execute_actions(actions))
        if len(tasks) > 0:
            await asyncio.gather(*tasks)

    async def message_event(self, msg, event_type):
        if msg.guild is None or msg.author == self.bot.user or msg.author.bot:
            return
        tasks = []
        delete = False
        async for channel, evt in self.get_events(msg.guild, 'on_message'):
            assert isinstance(evt, proto.MessageEvent)
            if ((evt.type | event_type) == 0 or
                    not is_valid_message_event(msg, channel, evt)):
                continue
            delete = delete or evt.delete_message
            actions = [copy.deepcopy(action) for action in evt.action]
            parameterize_actions(actions, msg.author, msg.guild, channel)
            tasks.append(self.execute_actions(actions))
        if len(tasks) > 0:
            await asyncio.gather(*tasks)
        if delete:
            await msg.delete()

    async def execute_actions(self, actions):
        for action in actions:
            await self.bot.actions.execute(action)

    async def get_events(self, guild, event_type):
        config = guild.config.auto
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


def setup(bot):
    bot.add_cog(Auto(bot))
    bot.add_cog(MessageFilter(bot))
