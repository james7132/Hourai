import aiohttp
import asyncio
import collections
import discord
import enum
import itertools
import logging
import pkgutil
import sys
import traceback
from discord.ext import commands
from hourai import config, web
from hourai.db import storage, proxies
from hourai.utils import fake, format, uvloop
from . import actions, extensions, state
from .context import HouraiContext

log = logging.getLogger(__name__)


class CounterKeys(enum.Enum):
    MESSAGES_RECIEVED = 0x100             # noqa: E221
    MESSAGES_DELETED = 0x101              # noqa: E221
    MESSAGES_EDITED = 0x102               # noqa: E221
    MEMBERS_JOINED = 0x200                # noqa: E221
    MEMBERS_LEFT = 0x201                  # noqa: E221
    MEMBERS_BANNED = 0x202                # noqa: E221
    MEMBERS_UNBANNED = 0x203              # noqa: E221
    MEMBERS_VERIFIED = 0x204              # noqa: E221
    MEMBERS_REJECTED = 0x205              # noqa: E221

    def __repr__(self):
        return self.name


class CommandInterpreter:

    def __init__(self, bot):
        self.bot = bot

    async def execute(self, ctx):
        raise NotImplementedError


class CommandExcecutor(CommandInterpreter):

    def __init__(self, interpreters):
        self.interpreters = interpreters

    async def execute(self, ctx):
        err = discord.errors.CommandNotFound(
            'Command "{ctx.invoked_with}" is not found')
        for interpreter in self.interpreters:
            try:
                await interpreter.execute(ctx, self)
                return
            except discord.errors.CommandNotFound:
                pass
            except Exception as e:
                err = e
                break
        ctx.bot.dispatch('command_error', err)


class DefaultCommandInterpreter(CommandInterpreter):

    async def execute(self, ctx, executor):
        bot = ctx.bot
        if ctx.command is not None:
            bot.dispatch('command', ctx)
            if await bot.can_run(ctx, call_once=True):
                await ctx.command.invoke(ctx)
            bot.dispatch('command_completion', ctx)
        elif ctx.invoked_with:
            raise discord.errors.CommandNotFound(
                'Command "{ctx.invoked_with}" is not found')


class AliasInterpreter(CommandInterpreter):
    pass


class Hourai(commands.AutoShardedBot):

    def __init__(self, *args, **kwargs):
        self.logger = log
        try:
            self.config = kwargs['config']
        except KeyError:
            raise ValueError(
                '"config" must be specified when initialzing Hourai.')
        # kwargs.setdefault('help_command', HouraiHelpCommand())
        self.storage = kwargs.get('storage') or storage.Storage(self.config)

        kwargs.setdefault('command_prefix', self.config.command_prefix)
        kwargs.setdefault('activity',
                          discord.Game(self.config.activity))
        kwargs.setdefault('fetch_offline_members', False)

        super().__init__(*args, **kwargs)
        self.http_session = aiohttp.ClientSession(loop=self.loop)
        self.action_manager = actions.ActionManager(self)

        self.guild_states = state.GuildStateMapping(self)

        # Counters
        self.guild_counters = collections.defaultdict(collections.Counter)
        self.channel_counters = collections.defaultdict(collections.Counter)
        self.user_counters = collections.defaultdict(collections.Counter)

        self.web_app_runner = None

    def create_storage_session(self):
        return self.storage.create_session()

    def run(self, *args, **kwargs):
        uvloop.try_install()
        super().run(*args, **kwargs)

    async def start(self, *args, **kwargs):
        try:
            await self.storage.init()
            await self.http_session.__aenter__()
            await self.start_web_api()
        except:
            log.exception("Failed to set up web API.")
            raise
        await super().start(*args, **kwargs)

    async def start_web_api(self):
        app = await web.create_app(self.config, bot=self)

        web_app_kwargs = {}
        log_format = self.config.logging.access_log_format
        if log_format:
            web_app_kwargs['access_log_format'] = log_format

        self.web_app_runner = aiohttp.web.AppRunner(app, **web_app_kwargs)
        port = self.config.web.port
        await self.web_app_runner.setup()
        await  aiohttp.web.TCPSite(self.web_app_runner, port=port).start()

    async def close(self):
        await super().close()
        if self.web_app_runner is not None:
            await self.web_app_runner.cleanup()
        await self.http_session.__aexit__(None, None, None)

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

        if not ctx.valid or ctx.prefix is None:
            return

        async with ctx:
            await self.invoke(ctx)
        log.debug(f'Command successfully executed: {msg}')

    def add_cog(self, cog):
        super().add_cog(cog)
        log.info("Cog {} loaded.".format(cog.__class__.__name__))

    async def on_error(self, event, *args, **kwargs):
        try:
            _, err, _ = sys.exc_info()
            err_msg = f'Error in {event} (args={args}, kwargs={kwargs}):'
            self.logger.exception(err_msg)
            self.dispatch('log_error', err_msg, err)
        except Exception:
            self.logger.exception('Waduhek')

    async def on_command_error(self, ctx, error):
        err_msg = None
        if isinstance(error, commands.CheckFailure):
            err_msg = str(error)
        elif isinstance(error, commands.UserInputError):
            err_msg = (str(error) + '\n') or ''
            err_msg += f"Try `~help {ctx.command} for a reference."
        elif isinstance(error, commands.CommandInvokeError):
            err_msg = ('An unexpected error has occured and has been reported.'
                       '\nIf this happens consistently, please consider filing'
                       ' a bug:\n<https://github.com/james7132/Hourai/issues>')
        if err_msg:
            await ctx.send(err_msg)

    def create_guild_proxy(self, guild):
        if guild is None:
            return None
        return proxies.GuildProxy(self, guild)

    def get_guild_config(self, guild, target_config):
        if guild is None:
            return None
        return self.create_guild_proxy(guild).get_config(target_config)

    def load_extension(self, module):
        try:
            super().load_extension(module)
            self.logger.info(f'Loaded extension: {module}')
        except Exception:
            self.logger.exception(f'Failed to load extension: {module}')

    def load_all_extensions(self, base_module=extensions):
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
