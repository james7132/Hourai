import aiohttp
import asyncio
import collections
import itertools
import logging
import pkgutil
import traceback
import sys
from discord.ext import commands
from . import config, actions
from .db import storage
from .utils import fake, format
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
        self.logger = log
        try:
            self.config = kwargs['config']
        except KeyError:
            raise ValueError(
                    '"config" must be specified when initialzing Hourai.')
        kwargs.setdefault('command_prefix', self.config.command_prefix)
        kwargs.setdefault('help_command', HouraiHelpCommand())
        self.storage = kwargs.get('storage') or storage.Storage(self.config)
        super().__init__(*args, **kwargs)
        self.http_session = aiohttp.ClientSession(loop=self.loop)
        self.action_executor = actions.ActionExecutor(self)

        # Counters
        self.guild_counters = collections.defaultdict(collections.Counter)
        self.channel_counters = collections.defaultdict(collections.Counter)
        self.user_counters = collections.defaultdict(collections.Counter)

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
        if isinstance(message, fake.FakeMessage):
            return ''
        return await super().get_prefix(message)

    def get_context(self, msg, *args, **kwargs):
        if isinstance(msg, fake.FakeMessage):
            msg._state = self._connection
        return super().get_context(msg, cls=HouraiContext, **kwargs)

    def get_automated_context(self, **kwargs):
        """
        Creates a fake context for automated uses. Mainly used to automatically
        run commands in response to configured triggers.
        """
        return self.get_context(fake.FakeMessage(**kwargs))

    async def process_commands(self, msg):
        if msg.author.bot:
            return

        ctx = await self.get_context(msg)

        if ctx.prefix is None:
            return

        async with ctx:
            await self.invoke(ctx)
        log.debug(f'Command successfully executed: {msg}')

    def add_cog(self, cog):
        super().add_cog(cog)
        log.info("Cog {} loaded.".format(cog.__class__.__name__))

    async def on_error(self, event, *args, **kwargs):
        error = f'Exception in event {event} (args={args}, kwargs={kwargs}):'
        self.logger.exception(error)
        _, error, _ = sys.exc_info()
        self.loop.create_task(self.send_owner_error(error))

    async def on_command_error(self, ctx, error):
        err_msg = None
        if isinstance(error, commands.CheckFailure):
            err_msg = str(error)
        elif isinstance(error, commands.UserInputError):
            err_msg = (str(error) + '\n') or ''
            err_msg += f"Try `~help {ctx.command} for a reference."
        elif isinstance(error, commands.CommandInvokeError):
            trace = traceback.format_exception(type(error), error,
                                               error.__traceback__)
            trace_str = '\n'.join(trace)
            log.error(f'In {ctx.command.qualified_name}:\n{trace_str}\n')
            self.loop.create_task(self.send_owner_error(error))
            err_msg = ('An unexpected error has occured and has been reported.'
                       '\nIf this happens consistently, please consider filing'
                       'a bug:\n<https://github.com/james7132/Hourai/issues>')
        log.debug(error)
        if err_msg:
            await ctx.send(err_msg)

    async def send_owner_error(self, error):
        owner = (await self.application_info()).owner
        trace = traceback.format_exception(type(error), error,
                                           error.__traceback__)
        trace_str = format.multiline_code('\n'.join(trace))
        await owner.send(trace_str)

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


class HouraiHelpCommand(commands.DefaultHelpCommand):

    async def send_bot_help(self, mapping):
        ctx = self.context
        bot = ctx.bot

        if bot.description:
            # <description> portion
            self.paginator.add_line(bot.description, empty=True)

        self.paginator.add_line('')
        self.paginator.add_line('Available modules:')

        no_category = '\u200b{0.no_category}:'.format(self)

        def get_category(command, *, no_category=no_category):
            cog = command.cog
            return cog.qualified_name + ':' if cog is not None else no_category
        filtered = await self.filter_commands(bot.commands, sort=True,
                                              key=get_category)
        to_iterate = itertools.groupby(filtered, key=get_category)

        for category, _ in to_iterate:
            self.paginator.add_line(' ' * 3 + category)

        command_name = self.clean_prefix + self.invoked_with
        note = (f"Type {command_name} module for more info on a module.\nYou"
                f" can also type {command_name} category for more info on a "
                f"category.")
        self.paginator.add_line()
        self.paginator.add_line(note)
        await self.send_pages()
