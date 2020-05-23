import asyncio
import collections
import copy
import discord
import hourai.utils as utils
import inspect
import re
import texttable
import traceback
import typing
from discord.ext import commands
from google.protobuf import text_format
from guppy import hpy
from hourai import extensions
from hourai.bot import CounterKeys
from hourai.cogs import BaseCog
from hourai.db import models, proto
from hourai.utils import hastebin, format


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
        """Group for searching Discord models."""
        pass

    @search.command(name="server")
    async def search_guild(self, ctx, regex):
        """Searches the servers the bot is on for matches."""
        regex = re.compile(regex)
        guilds = (f'{g.id}: {g.name}' for g in ctx.bot.guilds
                  if regex_multi_attr_match(regex, g, self.GUILD_CRITERIA))
        await ctx.send(format.multiline_code(format.vertical_list(guilds)))

    @search.command(name="user")
    async def search_user(self, ctx, regex):
        """Searches the users the bot can see for matches"""
        regex = re.compile(regex)
        usernames = ctx.session.query(models.Usernames).all()
        # Deduplicate entries
        users = {u.id: u for u in usernames
                 if regex_multi_attr_match(regex, u, self.USER_CRITERIA)}
        users = (f'{u.id}: {u.name}#{u.discriminator}' for _,
                 u in users.items())
        await ctx.send(format.multiline_code(format.vertical_list(users)))

    @commands.command()
    async def broadcast(self, ctx, *, message: str):
        """Broadcasts a message to all modlogs that Hourai is in."""
        async def broadcast_msg(guild):
            modlog = await ctx.bot.create_guild_proxy(guild).get_modlog()
            await modlog.send(content=guild.owner.mention +
                              '. **Announcement:**\n' + message)

        await asyncio.gather(*[
            broadcast_msg(guild) for guild in ctx.bot.guilds])

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
        """Evaluates a Python snippet and returns it."""
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
            result = str(result)
            if len(result) > 2000:
                result = await utils.hastebin.post(ctx.bot.http_session,
                                                   result)
            await ctx.send(f"Eval results for `{expr}`:\n```{result}```")
        except Exception:
            await ctx.send(f"Error when running eval of `{expr}`:\n"
                           f"```{str(traceback.format_exc())}```")

    @commands.command()
    async def heap(self, ctx):
        """Provides a dump of heap memory diagnositics."""
        heap = hpy().heap()
        output = str(heap)
        if len(output) > 1992:
            output = await utils.hastebin.post(ctx.bot.http_session, output)
        else:
            output = f"```\n{output}\n```"
        await ctx.send(output)

    @commands.group(name="config", invoke_without_command=True)
    @commands.guild_only()
    async def config(self, ctx):
        """Commands for managing custom guild configs."""
        pass

    @config.command(name="upload")
    async def config_upload(self, ctx, guild_id: typing.Optional[int] = None):
        """Uploads a config for a guild."""
        if len(ctx.message.attachments) <= 0:
            await ctx.send('Must provide a config file!')
            return
        config = proto.GuildConfig()
        text_format.Merge(await ctx.message.attachments[0].read(), config)

        guild_id = guild_id or ctx.guild.id
        await ctx.bot.storage.guild_configs.set(guild_id, config)
        await ctx.send('Config successfully uploaded.')

    @config.command(name="dump")
    async def config_dump(self, ctx, guild_id: typing.Optional[int] = None):
        """Dumps the config for a given server in Protobuf Text Format."""
        # TODO(james7132): Make the operation atomic
        config = proto.GuildConfig()

        guild_id = guild_id or ctx.guild.id

        config = await ctx.bot.storage.guild_configs.get(guild_id)
        output = text_format.MessageToString(config, indent=2)
        output = await hastebin.post(ctx.bot.http_session, output)
        await ctx.send(output)

    @commands.command()
    async def extractids(self, ctx, *, input_str: str):
        """Extracts all IDs from a provided string."""
        ids = re.findall(r'\d+', input_str)
        await ctx.send(format.vertical_list(ids))

    @commands.command()
    async def leave(self, ctx, *, guild_ids: int):
        for id in guild_ids:
            guild = ctx.bot.get_guild(id)
            if guild is None:
                continue
            await guild.leave()

    @commands.command()
    async def stats(self, ctx):
        """Provides statistics for each shard of the bot."""
        output = []
        latencies = dict(ctx.bot.latencies)
        columns = ('Shard', 'Guilds', 'Members', 'Channels', 'Roles',
                   'Music', 'Messages', 'Latency')
        shard_stats = {shard_id: Owner.get_shard_stats(ctx, shard_id)
                       for shard_id in latencies.keys()}
        table = texttable.Texttable()
        table.set_deco(texttable.Texttable.HEADER | texttable.Texttable.VLINES)
        table.set_cols_align(["r"] * len(columns))
        table.set_cols_valign(["t"] * len(columns))
        table.set_cols_valign(["t"] + ["i"] * (len(columns) - 1))
        table.header(columns)
        for shard_id, stats in sorted(shard_stats.items()):
            stats['Latency'] = latencies.get(shard_id) or 'N/A'
            table.add_row([shard_id] + [stats[key] for key in columns[1:]])

        output.append(table.draw())
        output.append('')
        output.append(f'discord.py: {discord.__version__}')
        await ctx.send(format.multiline_code(format.vertical_list(output)))

    @staticmethod
    def get_shard_stats(ctx, shard_id):
        counters = collections.Counter()
        counters['Shard'] = shard_id
        for guild in ctx.bot.guilds:
            if guild.shard_id != shard_id:
                continue
            guild_counts = ctx.bot.guild_counters[guild.id]
            counters['Guilds'] += 1
            counters['Members'] += guild.member_count
            counters['Roles'] += len(guild.roles)
            counters['Channels'] += len(guild.channels)
            counters['Messages'] += guild_counts[CounterKeys.MESSAGES_RECIEVED]
            if any(guild.me in vc.members for vc in guild.voice_channels):
                counters['Music'] += 1
        return counters


def setup(bot):
    bot.add_cog(Owner())
