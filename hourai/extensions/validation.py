import discord
import asyncio
from discord.ext import commands
from datetime import datetime, timedelta
from hourai import bot, db
from hourai.db import models

LOOKBACK = timedelta(days=1)
BATCH_SIZE = 10

def _get_validation_config(ctx):
    return ctx.session.query(models.GuildValidationConfig).get(ctx.guild.id)

def _is_invalid_member(member):
    is_new = member.created_at > datetime.utcnow() - LOOKBACK
    has_no_avatar = member.avatar is None
    return is_new or has_no_avatar

def _chunk_iter(src, chunk_size):
    chunk = []
    for val in src:
        chunk.append(val)
        if len(chunk) >= chunk_size:
            yield chunk
            chunk = []
    yield chunk


class Validation(bot.BaseCog):

    def __init__(self, bot):
        super().__init__()
        self.bot = bot

    @commands.guild_only()
    @commands.group(invoke_without_command=True)
    async def validation(self, ctx):
        pass

    @validation.command(name="setup")
    async def validation_setup(self, ctx, role: discord.Role, channel: discord.TextChannel):
        config = _get_validation_config(ctx) or models.GuildValidationConfig()
        config.guild_id = ctx.guild.id
        config.validation_role_id = role.id
        config.validation_channel_id = channel.id
        ctx.session.add(config)
        ctx.session.commit()
        await ctx.send('Validation configuration complete! Please run `~validation propogate` then `~validation lockdown` to complete setup.')

    @validation.command(name="propogate")
    @commands.bot_has_permissions(manage_roles=True)
    async def validation_propogate(self, ctx):
        config = _get_validation_config(ctx)
        if config is None:
            await ctx.send('No validation config was found. Please run `~valdiation setup`')
            return
        msg = await ctx.send('Propogating validation role...!')
        if not ctx.guild.chunked:
            await ctx.bot.request_offline_members(ctx.guild)
        role = ctx.guild.get_role(config.validation_role_id)
        member_count = len(ctx.guild.members)
        total_processed = 0
        async def add_role(member, role):
            try:
                await member.add_roles(role)
            except discord.errors.Forbidden:
                pass
        for chunk in _chunk_iter(ctx.guild.members, BATCH_SIZE):
            await asyncio.gather(*[add_role(mem, role) for mem in chunk])
            total_processed += len(chunk)
            await msg.edit(content=f'Propogation Ongoing ({total_processed}/{member_count})...')
        await msg.edit(content=f'Propogation conplete!')

    @validation.command(name="lockdown")
    @commands.bot_has_permissions(manage_channels=True)
    async def validation_lockdown(self, ctx):
        config = _get_validation_config(ctx)
        if config is None:
            await ctx.send('No validation config was found. Please run `~valdiation setup`')
            return
        msg = await ctx.send('Locking down all channels!')
        everyone_role = ctx.guild.default_role
        validation_role = ctx.guild.get_role(config.validation_role_id)

        def update_overwrites(channel, role, read=True):
            overwrites = dict(channel.overwrites)
            validation = overwrites.get(role) or discord.PermissionOverwrite()
            validation.update(read_messages=read, connect=read)
            return validation

        everyone_perms = everyone_role.permissions
        everyone_perms.update(read_messages=False, connect=False)

        tasks = []
        tasks += [ch.set_permissions(validation_role,
                                     update_overwrites(ch, validation_role))
                  for ch in ctx.guild.channels
                  if ch.id != config.validation_channel_id]
        tasks.append(validation_channel.set_permissions(role, update_overwrites(valdiation_channel, everyone_role, read=True)))
        tasks.append(validation_channel.set_permissions(role, update_overwrites(valdiation_channel, validation_role, read=False)))
        tasks.append(everyone_role.edit(permissions=everyone_perms))

        await asyncio.gather(*tasks)
        await msg.edit(f'Lockdown complete! Make sure your mods can read the validation channel!')

    @commands.Cog.listener()
    async def on_member_join(self, member):
        print('{} ({}) joined {} ({})'.format(member.name.encode('utf-8'), member.id,
            member.guild.name.encode('utf-8'), member.guild.id))
        session = self.bot.create_db_session()
        config = session.query(models.GuildValidationConfig).get(member.guild.id)
        if config is None:
            return
        if _is_invalid_member(member):
            print('{} ({}) rejected!'.format(member.name.encode('utf-8'), member.id))
            return
        print('{} ({}) verified!'.format(member.name.encode('utf-8'), member.id))
        role = member.guild.get_role(config.validation_role_id)
        await member.add_roles(role)
        # TODO(james7132): Add logging here

def setup(bot):
    bot.add_cog(Validation(bot))
