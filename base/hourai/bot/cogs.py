import discord
import typing
from functools import wraps
from discord.ext import commands

GUILD_TESTS = (lambda arg: arg.guild,
               lambda arg: arg.channel.guild,
               lambda arg: arg.message.channel.guild)


class CogLoadError(Exception):
    pass


class BaseCog(commands.Cog):
    pass


class PrivateCog(commands.Cog(command_attrs={"hidden": True})):
    """A cog that does not show any of it's commands in the help command."""
    pass


class GuildSpecificCog(BaseCog):
    """A cog that operates only on specific servers, provided as guild IDs at
    initialiation.
    """

    def __init__(self, bot, *, guilds=set()):
        super().__init__()
        self.bot = bot
        self.__allowed_guilds = set(guilds)

        for name, method in self.get_listeners():
            method_name = method.__name__
            setattr(self, method_name, self.__check_guilds(method))

    def cog_check(self, ctx):
        if ctx.guild is None:
            raise commands.NoPrivateMessage()
        if ctx.guild.id not in self.__allowed_guilds:
            raise commands.CheckFailure(
                message='This command can only be used in specific servers.')
        return True

    def __check_guilds(self, func):
        @wraps(func)
        async def _check_guilds(*args, **kwargs):
            for arg in args:
                guild_id = GuildSpecificCog.__get_guild_id(arg)
                if guild_id is not None and guild_id in self.__allowed_guilds:
                    await func(*args, **kwargs)
                    return
            for kwarg in kwargs.items():
                guild_id = GuildSpecificCog.__get_guild_id(kwarg)
                if guild_id is not None and guild_id in self.__allowed_guilds:
                    await func(*args, **kwargs)
                    return
        return _check_guilds

    @staticmethod
    def __get_guild_id(arg: typing.Any) -> int:
        if isinstance(arg, discord.Guild):
            return arg.id
        for test in GUILD_TESTS:
            try:
                return GuildSpecificCog.__get_guild_id(test(arg))
            except Exception:
                pass
        return None
