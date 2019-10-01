import asyncio
import copy
import hourai.utils as utils
import inspect
import re
import traceback
import typing
from discord.ext import commands
from google.protobuf import text_format
from guppy import hpy
from hourai import extensions
from hourai.cogs import BaseCog
from hourai.db import models, proto
from hourai.utils import hastebin


def regex_multi_attr_match(context, regex, attrs):
    return any(regex.search(func(context)) for func in attrs)


class Owner(BaseCog):

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
                  if regex_multi_attr_match(regex, g, self.GUILD_CRITERIA))
        await ctx.send(format.multiline_code(format.vertical_list(guilds)))

    @search.command(name="user")
    async def search_user(self, ctx, regex):
        regex = re.compile(regex)
        usernames = ctx.session.query(models.Usernames).all()
        # Deduplicate entries
        users = {u.id: u for u in usernames
                 if regex_multi_attr_match(regex, u, self.USER_CRITERIA)}
        users = (f'{u.id}: {u.name}#{u.discriminator}' for _,
                 u in users.items())
        await ctx.send(format.multiline_code(format.vertical_list(users)))

    @commands.command()
    async def reload(self, ctx,  *, extension: str):
        """Reloads the specified bot module."""
        extension = f'{extensions.__name__}.{extension}'
        try:
            ctx.bot.unload_extension(extension)
        except Exception:
            pass
        try:
            ctx.bot.load_extension(extension)
        except Exception as e:
            trace = utils.format.ellipsize(traceback.format_exc())
            err_type = type(e).__name__
            await ctx.send(f'**ERROR**: {err_type} - {e}\n```{trace}```')
        else:
            await utils.success(ctx)

    @commands.command()
    async def repeat(self, ctx, times: int, *, command):
        """Repeats a command a specified number of times."""
        msg = copy.copy(ctx.message)
        msg.content = command

        new_ctx = await ctx.bot.get_context(msg)
        new_ctx.db = ctx.db

        for i in range(times):
            await new_ctx.reinvoke()

    @commands.command()
    async def eval(self, ctx, *, expr: str):
        global_vars = {**globals(), **{
            'bot': ctx.bot,
            'msg': ctx.message,
            'channel': ctx.channel,
            'guild': ctx.guild,
            'guilds': ctx.bot.guilds,
            'users': ctx.bot.users,
            'dms': ctx.bot.private_channels,
            'members': ctx.bot.get_all_members(),
            'channels': ctx.bot.get_all_channels(),
        }}
        try:
            result = eval(expr, {}, global_vars)
            if inspect.isawaitable(result):
                result = await result
            await ctx.send(f"Eval results for `{expr}`:\n```{str(result)}```")
        except Exception:
            await ctx.send(f"Error when running eval of `{expr}`:\n"
                           f"```{str(traceback.format_exc())}```")

    @commands.command()
    async def heap(self, ctx):
        heap = hpy().heap()
        output = str(heap)
        if len(output) > 1992:
            output = await utils.hastebin.post(ctx.bot.http_session, output)
        else:
            output = f"```\n{output}\n```"
        await ctx.send(output)

    @commands.group(name="pconfig", invoke_without_command=True)
    @commands.guild_only()
    async def config(self, ctx):
        pass

    @config.command(name="upload")
    async def config_upload(self, ctx, guild_id: typing.Optional[int] = None):
        if len(ctx.message.attachments) <= 0:
            await ctx.send('Must provide a config file!')
            return
        config = proto.GuildConfig()
        text_format.Merge(await ctx.message.attachments[0].read(), config)

        guild_id = guild_id or ctx.guild.id

        tasks = []
        for cache, field in self.get_mapping(ctx.session.storage):
            if config.HasField(field):
                tasks.append(cache.set(guild_id, getattr(config, field)))
        await asyncio.gather(*tasks)
        await ctx.send('Config successfully uploaded.')

    @config.command(name="dump")
    async def config_dump(self, ctx, guild_id: typing.Optional[int] = None):
        # TODO(james7132): Make the operation atomic
        config = proto.GuildConfig()

        guild_id = guild_id or ctx.guild.id

        async def _get_field(cache, field):
            result = await cache.get(guild_id)
            if result is not None:
                getattr(config, field).CopyFrom(result)
        mapping = self.get_mapping(ctx.session.storage)
        await asyncio.gather(*[_get_field(c, f) for c, f in mapping])
        output = text_format.MessageToString(config, indent=2)
        output = await hastebin.post(ctx.bot.http_session, output)
        await ctx.send(output)

    def get_mapping(self, storage):
        return (
            (storage.logging_configs, 'logging'),
            (storage.validation_configs, 'validation'),
            (storage.auto_configs, 'auto'),
            (storage.moderation_configs, 'moderation'),
            (storage.music_configs, 'music'),
        )


def setup(bot):
    bot.add_cog(Owner())
