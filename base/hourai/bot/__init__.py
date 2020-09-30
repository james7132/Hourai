import aiohttp
import asyncio
import collections
import discord
import enum
import time
import logging
import pkgutil
import sys
from discord.state import ConnectionState
from discord.ext import commands
from hourai import config, web, utils
from hourai.db import storage, proxies
from hourai.utils import fake, uvloop
from . import actions, extensions
from .context import HouraiContext

log = logging.getLogger(__name__)

# Monkeypatch Hacks
__old_guild_add_member = discord.Guild._add_member
def should_cache_member(member):
    return utils.is_moderator(member) or member.id == member._state.user.id

def limit_cache_add_member(self, member, force=False):
    if member is not None and force or should_cache_member(member):
        __old_guild_add_member(self, member)
discord.Guild._add_member = limit_cache_add_member

__old_parse_guild_member_remove = ConnectionState.parse_guild_member_remove
def parse_guild_member_remove(self, data):
    __old_parse_guild_member_remove(self, data)
    self.dispatch('raw_member_remove', data)
ConnectionState.parse_guild_member_remove = parse_guild_member_remove

__old_parse_guild_member_update = ConnectionState.parse_guild_member_update
def parse_guild_member_update(self, data):
    __old_parse_guild_member_update(self, data)
    self.dispatch('raw_member_update', data)
ConnectionState.parse_guild_member_update = parse_guild_member_update

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
        self.storage = kwargs.get('storage') or storage.Storage(self.config)

        defaults = {
            'description': self.config.description,
            'command_prefix': self.config.command_prefix,
            'activity': discord.Game(self.config.activity),
            'help_command': HouraiHelpCommand(),
            'fetch_offline_members': False,
            'allowed_mentions': discord.AllowedMentions(
                everyone=False,
                users=True,
                roles=False),
            'intents': discord.Intents(
                bans=True,
                guilds=True,
                invites=True,
                members=True,
                messages=True,
                presences=True,
                reactions=True,
                typing=True,
                voice_states=True,
                emojis=False,
                integrations=False,
                webhooks=False),
        }
        for key, value, in defaults.items():
            kwargs.setdefault(key,value)

        super().__init__(*args, **kwargs)
        self.http_session = aiohttp.ClientSession(loop=self.loop)
        self.action_manager = actions.ActionManager(self)

        self.guild_proxies = {}

        # Counters
        self.bot_counters = collections.defaultdict(collections.Counter)
        self.guild_counters = collections.defaultdict(collections.Counter)
        self.channel_counters = collections.defaultdict(collections.Counter)
        self.user_counters = collections.defaultdict(collections.Counter)

        self.web_app_runner = None

    def create_storage_session(self):
        return self.storage.create_session()

    def dispatch(self, event, *args, **kwargs):
        self.bot_counters['events_dispatched'][event] += 1
        super().dispatch(event, *args, **kwargs)

    async def _run_event(self, coro, event_name, *args, **kwargs):
        if event_name.startswith('on_'):
            event_name = event_name[3:]
        self.bot_counters['events_run'][event_name] += 1
        start = time.time()
        try:
            await coro(*args, **kwargs)
        except asyncio.CancelledError:
            pass
        except Exception:
            try:
                await self.on_error(event_name, *args, **kwargs)
            except asyncio.CancelledError:
                pass
        runtime = time.time() - start
        self.bot_counters['event_total_runtime'][event_name] += runtime

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
        log.info(f'Starting bot...')
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
        await aiohttp.web.TCPSite(self.web_app_runner, port=port).start()

    async def close(self):
        await super().close()
        if self.web_app_runner is not None:
            await self.web_app_runner.cleanup()
        await self.http_session.__aexit__(None, None, None)

    async def on_guild_available(self, guild):
        log.info(f'Guild Available: {guild.id}')

    async def on_ready(self):
        log.info(f'Bot Ready: {self.user.name} ({self.user.id})')

    async def on_message(self, message):
        if message.author.bot:
            return
        await self.process_commands(message)

    async def on_guild_remove(self, guild):
        try:
            del self.guild_proxies[guild.id]
        except KeyError:
            pass

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
        log.info(f"Cog {cog.__class__.__name__} loaded.")

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
        if not err_msg:
            return

        prefix = (f"{ctx.author.mention} An error occured, and the bot does not"
                  f" have permissions to respond in #{ctx.channel.name}. "
                  f"Please double check the bot's permissions and try again. "
                  f"Original error message:\n\n")

        async def find_viable_channel(msg):
            if ctx.guild is None:
                return
            ch = discord.utils.find(lambda ch:
                    ch.permissions_for(ctx.guild.me).send_messages and
                    ch.permissions_for(ctx.author).read_messages,
                    ctx.guild.text_channels)
            await ch.send(prefix + msg)

        attempts = [
            lambda msg: ctx.send(msg),
            lambda msg: ctx.author.send(prefix + msg),
            find_viable_channel,
        ]

        for attempt in attempts:
            try:
                attempt(err_msg)
            except (discord.Forbidden, discord.NotFound):
                continue

    def get_guild_proxy(self, guild):
        try:
            return self.guild_proxies[guild.id]
        except AttributeError:
            return None
        except KeyError:
            if isinstance(guild, discord.Guild):
                self.guild_proxies[guild.id] = proxies.GuildProxy(self, guild)
            return self.guild_proxies.get(guild.id)

    async def get_guild_config(self, guild, target_config):
        if guild is None:
            return None
        return await self.get_guild_proxy(guild).config.get(target_config)

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
        bot = self.context.bot
        command_name = self.clean_prefix + self.invoked_with

        response = (
            f"**{bot.user.name}**\n"
            f"{bot.description}\n"
            f"For a full list of available commands, please see "
            f"<https://docs.hourai.gg/Commands>.\n"
            f"For more detailed usage information on any command, use "
            f"`{command_name} <command>`.\n\n"
            f"{bot.user.name} is a bot focused on automating security and "
            f"moderation with extensive configuration options. Most of the "
            f"advanced features are not directly accessible via commands. "
            f"Please see the full documentation at <https://docs.hourai.gg/>."
            f"\n\n If you find this bot useful, please vote for the bot: "
            f"<https://top.gg/bot/{bot.user.id}>")

        await self.context.send(response)
