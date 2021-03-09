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
from hourai.bot import CounterKeys, extensions, cogs
from hourai.db import models, proto
from hourai.utils import hastebin, format


def regex_multi_attr_match(context, regex, attrs):
    return any(regex.search(func(context)) for func in attrs)


class Owner(cogs.BaseCog):

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
        if not await ctx.bot.is_owner(ctx.author):
            raise commands.NotOwner()
        return True

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
            msg = f"<@{guild.owner_id}>. **Announcement:**\n{message}"
            await ctx.guild.modlog.send(content=msg)

        await asyncio.gather(*[
            broadcast_msg(guild) for guild in ctx.bot.guilds])

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
            result = await hastebin.str_or_hastebin_link(ctx.bot, result)
            await ctx.send(f"Eval results for `{expr}`:\n```{result}```")
        except Exception:
            await ctx.send(f"Error when running eval of `{expr}`:\n"
                           f"```{str(traceback.format_exc())}```")

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

        guild_id = guild_id or ctx.guild.id
        guild = ctx.bot.get_guild(guild_id)
        if guild is None:
            await ctx.send(f"No such guild found: {guild_id}")
            return

        config = proto.GuildConfig()
        text_format.Merge(await ctx.message.attachments[0].read(), config)
        guild.config = config
        await guild.flush_config()
        await ctx.send('Config successfully uploaded.')

    @config.command(name="dump")
    async def config_dump(self, ctx, guild_id: typing.Optional[int] = None):
        """Dumps the config for a given server in Protobuf Text Format."""
        # TODO(james7132): Make the operation atomic
        guild_id = guild_id or ctx.guild.id
        guild = ctx.bot.get_guild(guild_id)
        if guild is None:
            await ctx.send(f"No such guild found: {guild_id}")
            return

        await guild.refresh_config()
        output = text_format.MessageToString(guild.config, indent=2)
        filename = f"{ctx.guild.name.replace(' ', '_')}.pbtxt"
        await ctx.send(
                file=utils.str_to_discord_file(output, filename=filename))

    @commands.command()
    async def events(self, ctx):
        """Provides debug informatioo about the events run by the bot."""
        counters = ctx.bot.bot_counters
        columns = ('Event', '# Dispatched', '# Run', 'Total Runtime',
                   'Average Time')
        keys = set()
        for key in ('events_dispatched', 'events_run', 'event_total_runtime'):
            keys.update(counters[key].keys())

        table = texttable.Texttable()
        table.set_deco(texttable.Texttable.HEADER | texttable.Texttable.VLINES)
        table.set_cols_align(["r"] * len(columns))
        table.set_cols_valign(["t"] + ["i"] * (len(columns) - 1))
        table.header(columns)
        for key in sorted(keys):
            runtime = counters['event_total_runtime'][key]
            run_count = counters['events_run'][key]
            avg_runtime = runtime / run_count if run_count else "N/A"
            table.add_row([key, counters['events_dispatched'][key],
                           run_count, runtime, avg_runtime])

        output = await hastebin.str_or_hastebin_link(ctx.bot, table.draw())
        await ctx.send(format.multiline_code(output))

    @commands.command()
    async def stats(self, ctx):
        """Provides statistics for each shard of the bot."""
        output = []
        latencies = dict(ctx.bot.latencies)
        columns = ('Shard', 'Guilds', 'Total Members', 'Loaded Members',
                   'Music', 'Messages', 'Latency')
        shard_stats = {shard_id: Owner.get_shard_stats(ctx, shard_id)
                       for shard_id in latencies.keys()}
        table = texttable.Texttable()
        table.set_deco(texttable.Texttable.HEADER | texttable.Texttable.VLINES)
        table.set_cols_align(["r"] * len(columns))
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
            counters['Total Members'] += guild.member_count
            counters['Loaded Members'] += len(guild.members)
            counters['Messages'] += guild_counts[CounterKeys.MESSAGES_RECIEVED]
            if any(guild.me.id in vc.voice_states
                   for vc in guild.voice_channels):
                counters['Music'] += 1
        return counters


def setup(bot):
    bot.add_cog(Owner())
