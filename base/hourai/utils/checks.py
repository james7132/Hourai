from discord.ext import commands
from hourai import utils


def is_moderator():
    def predicate(ctx):
        return utils.is_moderator(ctx.author) or ctx.bot.is_owner(ctx.author)
    return commands.check(predicate)
