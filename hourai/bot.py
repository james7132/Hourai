import aiohttp
import asyncio
import discord
import logging
import pkgutil
import traceback
from functools import wraps
from discord.ext import commands
from hourai.db import proxies

GUILD_TESTS = (lambda arg: arg.guild,
               lambda arg: arg.channel.guild,
               lambda arg: arg.message.channel.guild)

log = logging.getLogger(__name__)

_FAKE_MESSAGE_ATTRS = (
    'content', 'channel', 'guild', 'author', '_state'
)

class FakeMessage:

    def __init__(self, **kwargs):
        msg = kwargs.pop('message', None)
        for attr in _FAKE_MESSAGE_ATTRS:
            if msg is not None and attr not in kwargs:
                setattr(self, attr, getattr(msg, attr, None))
            else:
                setattr(self, attr, kwargs.pop(attr, None))


def action_command(func):
    @wraps(func)
    async def command(*args, **kwargs):
        async for action in func(*args, **kwargs):
            result = await action.execute(ctx.bot)
            # TODO(james7132): do something with this
        # TODO(james7132): add response here
    return command


def _get_guild_id(arg):
    if isinstance(arg, discord.Guild):
        return arg.id
    for test in GUILD_TESTS:
        try:
            return _get_guild_id(test(arg))
        except:
            pass
    return None


class BaseCog(commands.Cog):

    def __init__(self):
        log.info("Cog {} loaded.".format(self.__class__.__name__))

class PrivateCog(commands.Cog(command_attrs={"hidden": True})):
    """
    A cog that does not show any of it's commands in the help command.
    """

    def __init__(self):
        log.info("Cog {} loaded.".format(self.__class__.__name__))

class GuildSpecificCog(BaseCog):
    """
    A cog that operates only on specific servers, provided as guild IDs at
    initialiation.
    """

    def __init__(self, bot, *, guilds=set()):
        super().__init__()
        self.bot = bot
        self.__allowed_guilds = set(g if isinstance(g, int) else g.id
                                    for g in guilds)

        for name, method in self.get_listeners():
            method_name = method.__name__
            setattr(self, method_name, self.__check_guilds(method))

    def cog_check(self, ctx):
        return ctx.guild is not None and ctx.guild.id in self.__allowed_guilds

    def __check_guilds(self, func):
        @wraps(func)
        async def _check_guilds(*args, **kwargs):
            for arg in args:
                guild_id = _get_guild_id(arg)
                if guild_id is not None and guild_id in self.__allowed_guilds:
                    await func(*args, **kwargs)
                    return
            for kwarg in kwargs.items():
                guild_id = __get_guild_id(kwarg)
                if guild_id is not None and guild_id in self.__allowed_guilds:
                    await func(*args, **kwargs)
                    return
        return _check_guilds


class HouraiContext(commands.Context):

    def __init__(self,  **attrs):
        super().__init__(**attrs)

    def __enter__(self):
        self.session = self.bot.create_db_session()
        return self

    def __exit__(self, exc_type, exc, traceback):
        if self.session is None:
            return
        if exc is None:
            self.session.commit()
        else:
            self.session.rollback()

    @property
    def is_automated(self):
        return isinstance(self.message, FakeMessage)

    @property
    def logger(self):
        return self.bot.logger

    def get_guild_proxy(self, guild=None):
        return proxies.GuildProxy(guild or self.guild, self.session)

    def get_automated_context(self):
        return self.bot.get_automated_context(message=self.message)

class Hourai(commands.AutoShardedBot):

    def __init__(self, *args, **kwargs):
        self.logger = log
        self.session_class = kwargs.pop('session_class', None)
        super().__init__(*args, **kwargs)

    def create_db_session(self):
        return self.session_class()

    async def on_ready(self):
        log.info(f'Bot Ready: {self.user.name} ({self.user.id})')

    async def on_message(self, message):
        if message.author.bot:
            return
        await self.process_commands(message)

    async def get_prefix(self, message):
        if isinstance(message, FakeMessage):
            return ''
        return await super().get_prefix(message)

    def get_context(self, msg, *args, **kwargs):
        if isinstance(msg, FakeMessage):
            msg._state = self._connection
        return super().get_context(msg, cls=HouraiContext, **kwargs)

    def get_automated_context(self, **kwargs):
        """
        Creates a fake context for automated uses. Mainly used to automatically
        run commands in response to configured triggers.
        """
        return self.get_context(FakeMessage(**kwargs))

    async def process_commands(self, msg):
        if msg.author.bot:
            return

        ctx = await self.get_context(msg)
        with ctx:
            await self.invoke(ctx)

    async def on_guild_available(self, guild):
        self.logger.info(f'Guild available: {guild.name}')

    async def on_guild_unavailable(self, guild):
        self.logger.info(f'Guild unavailable: {guild.name}')

    async def on_error(self, event, *args, **kwargs):
        self.logger.exception(f'Exception in event {event}:')

    async def on_command_error(self, ctx, error):
        if isinstance(error, commands.NoPrivateMessage):
            await ctx.author.send('This command cannot be used in private messages.')
        elif isinstance(error, commands.DisabledCommand):
            await ctx.author.send('Sorry. This command is disabled and cannot be used.')
        elif isinstance(error, commands.CommandInvokeError):
            trace = traceback.format_exception(type(error), error,
                                               error.__traceback__)
            trace_str = '\n'.join(trace)
            log.error(f'In {ctx.command.qualified_name}:\n{trace_str}\n')

    async def execute_actions(self, actions):
        tasks = (action.execute(self) for action in actions)
        await kasyncio.gather(*tasks)

    def load_extension(self, module):
        try:
            super().load_extension(module)
            self.logger.info(f'Loaded extension: {module}')
        except:
            self.logger.exception(f'Failed to load extension: {module}')

    def load_all_extensions(self, base_module):
        modules = pkgutil.iter_modules(base_module.__path__,
                                       base_module.__name__ + '.')
        for module in modules:
            self.load_extension(module.name)

    def get_all_matching_members(self, user):
       return (m for m in self.get_all_members() if m.id == user.id)
