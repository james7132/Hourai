from discord.ext import commands
from hourai import utils


def is_moderator():
    async def predicate(ctx):
        if ctx.guild is None:
            raise commands.NoPrivateMessage()
        if not (utils.is_moderator(ctx.author) or
                await ctx.bot.is_owner(ctx.author)):
            raise commands.CheckFailure(
                    message='You are not a moderator of this server.')
        return True
    return commands.check(predicate)
