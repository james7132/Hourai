import aiohttp
import asyncio
import logging
import pkgutil
import traceback
from discord.ext import commands
from . import config
from .db import storage
from .utils.fake import FakeMessage
from .cogs import PrivateCog
from .context import HouraiContext

log = logging.getLogger(__name__)


class CogLoadError(Exception):
    pass


class CommandInterpreter:

    def __init__(self, bot):
        self.bot = bot

    async def execute(self, ctx):
        raise NotImplementedError


class AliasInterpreter(CommandInterpreter):
    pass


class CustomCommandInterpreter(CommandInterpreter):
    pass


class DefaultCommandInterpreter(CommandInterpreter):

    async def execute(self, ctx):
        await self.bot.invoke(ctx)


class Hourai(commands.AutoShardedBot):

    def __init__(self, *args, **kwargs):
        try:
            self.config = kwargs['config']
        except KeyError:
            raise ValueError(
                    '"config" must be specified when initialzing Hourai.')
        kwargs.setdefault('command_prefix', self.config.command_prefix)
        self.logger = log
        self.storage = kwargs.get('storage') or storage.Storage(self.config)
        super().__init__(*args, **kwargs)
        self.http_session = aiohttp.ClientSession(loop=self.loop)

    def create_storage_session(self):
        return self.storage.create_session()

    async def start(self, *args, **kwargs):
        await self.storage.init()
        await self.http_session.__aenter__()
        await super().start(*args, **kwargs)

    async def close(self):
        await self.http_session.__aexit__()
        await super().close()

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

        if ctx.prefix is None:
            return

        async with ctx:
            await self.invoke(ctx)

    def add_cog(self, cog):
        super().add_cog(cog)
        log.info("Cog {} loaded.".format(cog.__class__.__name__))

    async def on_error(self, event, *args, **kwargs):
        error = f'Exception in event {event} (args={args}, kwargs={kwargs}):'
        self.logger.exception(error)

    async def on_command_error(self, ctx, error):
        err_msg = None
        if isinstance(error, commands.NoPrivateMessage):
            err_msg = 'This command cannot be used in private messages.'
        elif isinstance(error, commands.DisabledCommand):
            err_msg = 'Sorry. This command is disabled and cannot be used.'
        elif isinstance(error, commands.CommandInvokeError):
            trace = traceback.format_exception(type(error), error,
                                               error.__traceback__)
            trace_str = '\n'.join(trace)
            log.error(f'In {ctx.command.qualified_name}:\n{trace_str}\n')
        if err_msg is not None:
            await ctx.send(err_msg)

    async def execute_actions(self, actions):
        tasks = (action.execute(self) for action in actions)
        await asyncio.gather(*tasks)

    def load_extension(self, module):
        try:
            super().load_extension(module)
            self.logger.info(f'Loaded extension: {module}')
        except Exception:
            self.logger.exception(f'Failed to load extension: {module}')

    def load_all_extensions(self, base_module):
        disabled_extensions = self.get_config_value('disabled_extensions',
                                                    type=tuple, default=())
        modules = pkgutil.iter_modules(base_module.__path__,
                                       base_module.__name__ + '.')
        for module in modules:
            if module.name not in disabled_extensions:
                self.load_extension(module.name)

    def spin_wait_until_ready(self):
        while not self.is_ready():
            pass

    def get_all_matching_members(self, user):
        return (m for m in self.get_all_members() if m.id == user.id)

    def get_config_value(self, *args, **kwargs):
        return config.get_config_value(self.config, *args, **kwargs)
