import hourai.util as util
import hourai.config as config
import sys
import traceback
import copy
from discord.ext import commands


class Owner(util.BaseCog):

    def __init__(self, bot):
        super().__init__(bot)

    async def __local_check(self, ctx):
        return await self.bot.is_owner(ctx.author)

    @commands.command(hidden=True)
    async def kill(self, ctx):
        """Kills the bot. """
        await util.success(ctx)
        sys.exit(0)

    @commands.command(hidden=True)
    async def reload(self, ctx,  *, extension: str):
        """Reloads the specified bot module."""
        extension = config.EXTENSION_PREFIX + extension
        try:
            self.bot.unload_extension(extension)
            self.bot.load_extension(extension)
        except Exception as e:
            trace = traceback.format_tb(error.original.__traceback__)
            await ctx.send(f'**ERROR**: {type(e).__name__} - {e}\n```{trace}```)')
        else:
            await util.success(ctx)

    @commands.command(hidden=True)
    async def repeat(self, ctx, times: int, *, command):
        """Repeats a command a specified number of times."""
        msg = copy.copy(ctx.message)
        msg.content = command

        new_ctx = await self.get_context(msg, cls=context.Context)
        new_ctx.db = ctx.db

        for i in range(times):
            await new_ctx.reinvoke()

def setup(bot):
    bot.add_cog(Owner(bot))
