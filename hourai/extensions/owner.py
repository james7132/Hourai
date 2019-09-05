import hourai.utils as utils
import hourai.config as config
import sys
import traceback
import copy
from hourai import bot, extensions
from discord.ext import commands


class Owner(bot.BaseCog):

    async def cog_check(self, ctx):
        return await ctx.bot.is_owner(ctx.author)

    # @commands.command()
    # async def kill(self, ctx):
        # """Kills the bot. """
        # await utils.success(ctx)
        # await ctx.bot.logout()

    @commands.group()
    async def s(self, ctx):
        pass

    @s.command(name='info')
    async def server_search(self, ctx, id: int):
        guild = ctx.bot.get_guild(id)
        if guild is None:
            await ctx.send('Server for ID {} not found.'.format(id))
            return
        await ctx.send('{} ({})'.format(guild.name, guild.id))

    @commands.command()
    async def reload(self, ctx,  *, extension: str):
        """Reloads the specified bot module."""
        extension = f'{extensions.__name__}.{extension}'
        try:
            ctx.bot.unload_extension(extension)
        except Exception as e:
            pass
        try:
            ctx.bot.load_extension(extension)
        except Exception as e:
            trace = traceback.format_exc()
            await ctx.send(f'**ERROR**: {type(e).__name__} - {e}\n```{trace}```')
        else:
            await utils.success(ctx)

    @commands.command()
    async def repeat(self, ctx, times: int, *, command):
        """Repeats a command a specified number of times."""
        msg = copy.copy(ctx.message)
        msg.content = command

        new_ctx = await ctx.bot.get_context(msg, cls=context.Context)
        new_ctx.db = ctx.db

        for i in range(times):
            await new_ctx.reinvoke()

def setup(bot):
    bot.add_cog(Owner())
