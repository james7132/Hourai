import re
import hourai.utils as utils
import hourai.config as config
import sys
import traceback
import copy
from hourai import bot, extensions
from hourai.db import models
from discord.ext import commands


class Owner(bot.BaseCog):

    GUILD_CRITERIA = (
        lambda g: g.name,
        lambda g: str(g.id)
    )

    USER_CRITERIA = (
        lambda u: u.name,
        lambda u: str(u.id),
        lambda u: (f'{u.discriminator:0>4d}' if u.discriminator is not None
                   else 'None'),
    )

    async def cog_check(self, ctx):
        return await ctx.bot.is_owner(ctx.author)

    @commands.group()
    async def search(self, ctx, regex):
        pass

    @search.command(name="server")
    async def search_guild(self, ctx, regex):
        regex = re.compile(regex)
        guilds = (f'{g.id}: {g.name}' for g in ctx.bot.guilds
                  if any(regex.search(func(g)) for func in self.GUILD_CRITERIA))
        await ctx.send(format.multiline_code(format.vertical_list(guilds)))

    @search.command(name="user")
    async def search_user(self, ctx, regex):
        regex = re.compile(regex)
        query = ctx.session.query(models.Usernames).all()
        # Deduplicate entries
        users = {u.id: u for u in usernames
                 if any(re.search(func(u)) for func in self.USER_CRITERIA)}
        users = (f'{u.id}: {u.name}#{u.discriminator}' for _, u in users.items())
        await ctx.send(format.multiline_code(format.vertical_list(users)))

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
