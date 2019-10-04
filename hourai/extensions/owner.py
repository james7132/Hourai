import copy
import hourai.utils as utils
import inspect
import re
import traceback
import typing
import sqlite3
import logging
import yaml
try:
    from yaml import CLoader as Loader, CDumper as Dumper
except ImportError:
    from yaml import Loader, Dumper
from google.protobuf.message import DecodeError
from discord.ext import commands
from google.protobuf import text_format
from guppy import hpy
from hourai import extensions
from hourai.cogs import BaseCog
from hourai.db import models, proto
from hourai.utils import hastebin, format


log = logging.getLogger(__name__)


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
        await ctx.bot.storage.guild_configs.set(guild_id, config)
        await ctx.send('Config successfully uploaded.')

    @config.command(name="dump")
    async def config_dump(self, ctx, guild_id: typing.Optional[int] = None):
        # TODO(james7132): Make the operation atomic
        config = proto.GuildConfig()

        guild_id = guild_id or ctx.guild.id

        config = await ctx.bot.storage.guild_configs.get(guild_id)
        output = text_format.MessageToString(config, indent=2)
        output = await hastebin.post(ctx.bot.http_session, output)
        await ctx.send(output)

    @commands.command()
    async def extractids(self, ctx, *, input_str: str):
        ids = re.findall(r'\d+', input_str)
        await ctx.send(format.vertical_list(ids))

    @commands.command()
    async def migrate(self, ctx, old_sql: str):
        for config in ctx.session.query(models.LoggingConfig).all():
            guild_id = config.guild_id
            try:
                config_proto = await ctx.bot.storage.logging_configs.get(guild_id)
            except DecodeError:
                config_proto = None
            config_proto = config_proto or proto.LoggingConfig()
            for attr in ('modlog_channel_id', 'log_deleted_messages'):
                setattr(config_proto, attr, getattr(config, attr))
            await ctx.bot.storage.logging_configs.set(
                    config.guild_id, config_proto)
        await ctx.send('Migrated Logging Configs')

        for config in ctx.session.query(models.GuildValidationConfig).all():
            guild_id = config.guild_id
            try:
                config_proto = await ctx.bot.storage.validation_configs.get(guild_id)
            except DecodeError:
                config_proto = None
            config_proto = config_proto or proto.ValidationConfig()
            config_proto.enabled = True
            config_proto.role_id = config.validation_role_id
            if config.is_propogated:
                config_proto.kick_unvalidated_users_after = 21600
            await ctx.bot.storage.validation_configs.set(
                    config.guild_id, config_proto)
        await ctx.send('Migrated Validation Configs')

        conn = sqlite3.connect(old_sql)
        c = conn.cursor()
        announce_configs = {}
        log.debug('Migrating Announce Channels')
        for row in c.execute('select * from channels;'):
            id, ban, guild_id, join, leave, stream, voice = row
            if guild_id is None:
                continue
            is_needed = False
            for attr in ('ban', 'join', 'leave', 'stream', 'voice'):
                val = locals()[attr] != 0
                locals()[attr] = val
                is_needed = is_needed or val
            if not is_needed:
                continue
            config_proto = announce_configs.setdefault(guild_id,
                    proto.AnnouncementConfig())
            if ban:
                config_proto.bans.channel_ids.append(id)
                config_proto.bans.channel_ids.sort()
            if join:
                config_proto.joins.channel_ids.append(id)
                config_proto.joins.channel_ids.sort()
            if leave:
                config_proto.leaves.channel_ids.append(id)
                config_proto.leaves.channel_ids.sort()
            if stream:
                config_proto.streams.channel_ids.append(id)
                config_proto.streams.channel_ids.sort()
            if voice:
                config_proto.voice.channel_ids.append(id)
                config_proto.voice.channel_ids.sort()
            log.debug(f'Migrated Channel: {id}, {guild_id}')
        for id, config_proto in announce_configs.items():
            await ctx.bot.storage.announce_configs.set(id, config_proto)
        await ctx.send('Migrated Announce Configs')

        role_configs = {}
        log.debug('Migrating Self Serve Roles')
        for row in c.execute('select * from roles;'):
            id, guild_id, self_serve = row
            if guild_id is None:
                continue
            is_needed = False
            for attr in ('self_serve',):
                val = locals()[attr] != 0
                locals()[attr] = val
                is_needed = is_needed or val
            if not is_needed:
                continue
            config_proto = role_configs.setdefault(guild_id, proto.RoleConfig())
            if self_serve:
                config_proto.self_serve_role_ids.append(id)
                config_proto.self_serve_role_ids.sort()
            log.debug(f'Migrated Role: {id}, {guild_id}')
        for id, config_proto in role_configs.items():
            await ctx.bot.storage.role_configs.set(id, config_proto)
        await ctx.send('Migrated Role Configs')

        log.debug('Migrating Aliases')
        for row in c.execute('select * from commands;'):
            guild_id, name, response = row
            if guild_id is None:
                continue
            model = ctx.session.query(models.Alias).filter_by(
                    guild_id=guild_id,
                    name=name).first()
            if model is None:
                model = models.Alias(guild_id=guild_id, name=name)
            model.content = "echo " + response
            ctx.session.add(model)
            log.debug(f'Migrated Alias: {guild_id}, {name}')
        for row in c.execute('select * from custom_config;'):
            guild_id, custom_config = row
            if guild_id is None:
                continue
            try:
                yaml_blob = yaml.load(custom_config)
                if 'aliases' not in yaml_blob:
                    continue
                for name, response in yaml_blob['aliases'].items():
                    model = ctx.session.query(models.Alias).filter_by(
                            guild_id=guild_id,
                            name=name).first()
                    if model is None:
                        model = models.Alias(guild_id=guild_id, name=name)
                    model.content = response
                    ctx.session.add(model)
                log.debug(f'Migrated Alias: {guild_id}, {name}')
            except Exception:
                log.exception('Error while loading YAML document:')
        ctx.session.commit()
        await ctx.send('Migrated Aliases')


def setup(bot):

    bot.add_cog(Owner())
