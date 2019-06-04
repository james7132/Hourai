import aiohttp
import asyncio
import discord
import logging
import traceback
from functools import wraps
from discord.ext.commands import Bot, Context, Cog, AutoShardedBot

GUILD_TESTS = (lambda arg: arg.guild,
               lambda arg: arg.channel.guild,
               lambda arg: arg.message.channel.guild)

log = logging.getLogger(__name__)


def _get_guild_id(arg):
    if isinstance(arg, discord.Guild):
        return arg.id
    for test in GUILD_TESTS:
        try:
            return _get_guild_id(test(arg))
        except:
            pass
    return None


class BaseCog(Cog):

    def __init__(self):
        print("Cog {} loaded.".format(self.__class__.__name__))


class GuildSpecificCog(BaseCog):

    def __init__(self, *, guilds=set()):
        super().__init__()
        self.__allowed_guilds = set(g if isinstance(g, int) else g.id
                                    for g in guilds)
        print(self.__allowed_guilds)

        for name, method in self.get_listeners():
            method_name = method.__name__
            print(method_name)
            setattr(self, method_name, self.__check_guilds(method))

    def cog_check(self, ctx):
        return ctx.guild is not None and ctx.guild.id in self.__allowed_guilds

    def __check_guilds(self, func):
        @wraps(func)
        async def _check_guilds(*args, **kwargs):
            for arg in args:
                guild_id = _get_guild_id(arg)
                print(guild_id, self.__allowed_guilds)
                if guild_id is not None and guild_id in self.__allowed_guilds:
                    await func(*args, **kwargs)
                    return
            for kwarg in kwargs.items():
                guild_id = __get_guild_id(kwarg)
                print(guild_id, self.__allowed_guilds)
                if guild_id is not None and guild_id in self.__allowed_guilds:
                    await func(*args, **kwargs)
                    return
        return _check_guilds


class HouraiContext(Context):

    def __init__(self, session_class, **attrs):
        super().__init__(**attrs)
        self.session_class = session_class
        self.session = None

    def __enter__(self):
        self.session = self.session_class()
        return self

    def __exit__(self, exc_type, exc, traceback):
        if exc is None:
            self.session.commit()
        else:
            self.session.rollback()


class Hourai(AutoShardedBot):

    async def on_ready(self):
        log.info(f'Bot Ready: {self.user.name} ({self.user.id})')

    async def on_message(self, message):
        if message.author.bot:
            return
        await self.process_commands(message)

    async def process_commands(self, message):
        ctx = await self.get_context(message,
                                     cls=HouraiContext,
                                     session_class=self.session_class)

        if ctx.command is None:
            return

        with ctx:
            await self.invoke(ctx)

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
