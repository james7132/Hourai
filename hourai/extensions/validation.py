import discord
import asyncio
import humanize
import traceback
import re
from discord.ext import tasks, commands
from datetime import datetime, timedelta
from hourai import bot, db, utils
from hourai.db import models, proxies
from hourai.utils import format

PURGE_LOOKBACK = timedelta(hours=6)
PURGE_DM = """
You have been kicked from {} due to not being verified within sufficient time.
If you feel this is in error, please contact a mod regarding this.
"""
BATCH_SIZE = 10
MINIMUM_GUILD_SIZE = 150

def _split_camel_case(val):
    return re.sub('([a-z])([A-Z0-9])', '$1 $2', val).split()

def _generalize_filter(filter_value):
    filter_value = re.escape(filter_value)
    def _generalize_character(char):
        return char + '+' if char.isalnum() else char
    return '(?i)' + ''.join(_generalize_character(char) for char in filter_value)

class Validator():

    def get_rejection_reasons(self, bot, member):
        return iter(())

class NameMatchValidator(Validator):

    def __init__(self, *, prefix, filter_func, subfield=None, member_selector=None):
        self.filter = filter_func
        self.subfield = subfield or (lambda m: m.name)
        self.member_selector = member_selector or (lambda m: m.name)

    def get_rejection_reasons(self, bot, member):
        member_names = {}
        for guild_member in filter(self.filter, member.guild.members):
            name = self.member_selector(guild_member) or ''
            member_names.update({
                p: _generalize_filter(p) for p in _split_camel_case(name)
            })
        field_value = self.subfield(member)
        for filter_name, regex in member_names.items():
            if re.search(regex, field_value):
                yield prefix + 'Matches: `{}`'.format(filter_name)

class StringFilterValidator(Validator):

    def __init__(self, *, prefix, filters, subfield=None):
        self.prefix = prefix or ''
        self.filters = [(f, re.compile(_generalize_filter(f))) for f in filters]
        self.subfield = subfield or (lambda m: m.name)
        print(self.filters)

    def get_rejection_reasons(self, bot, member):
        field_value = self.subfield(member)
        for filter_name, regex in self.filters:
            if regex.search(field_value):
                yield prefix + 'Matches: `{}`'.format(filter_name)

class NewAccountValidator(Validator):

    def __init__(self, *, lookback):
        self.lookback = lookback

    def get_rejection_reasons(self, bot, member):
        if member.created_at > datetime.utcnow() - self.lookback:
            yield "Account created less than {}".format(humanize.naturaltime(self.lookback))

class NoAvatarValidator(Validator):

    def get_rejection_reasons(self, bot, member):
        if member.avatar is None:
            yield "User has no avatar."

class BannedUserValidator(Validator):

    def __init__(self, *, min_guild_size):
        self.min_guild_size = min_guild_size

    def get_rejection_reasons(self, bot, member):
        db_session = bot.create_db_session()
        bans = db_session.query(models.Ban).filter_by(user_id=member.id).all()
        ban_guilds = ((ban, bot.get_guild(ban.guild_id)) for ban in bans
                      if self._is_valid_guild(ban, bot.get_guild(ban.guild_id)))
        reasons = set(ban.reason for ban, guild in ban_guilds)
        if reasons == set([None]):
            yield "Banned on another server."
        else:
            yield from ("Banned on another server. Reason: `{}`.".format(r)
                        for r in reasons)

    def _is_valid_guild(self, ban, guild):
        return guild is not None and guild.member_count >= self.min_guild_size

# TODO(james7132): Add filter for pornographic or violent avatars
VALIDATORS = (NewAccountValidator(lookback=timedelta(days=1)),
              NoAvatarValidator(),
              BannedUserValidator(min_guild_size=MINIMUM_GUILD_SIZE),

              # Filter usernames and nicknames that match moderator users.
              NameMatchValidator(prefix='Username matches moderator\'s. ',
                                filter_func=utils.is_moderator),
              NameMatchValidator(prefix='Username matches moderator\'s. ',
                                filter_func=utils.is_moderator,
                                member_selector=lambda m: m.nick),

              # Filter usernames and nicknames that match bot users.
              NameMatchValidator(prefix='Username matches bot\'s. ',
                                filter_func=lambda m: m.bot),
              NameMatchValidator(prefix='Username matches bot\'s. ',
                                filter_func=lambda m: m.bot,
                                 member_selector=lambda m: m.nick),

              # Filter offensive usernames.
              StringFilterValidator(
                  prefix='Offensive username. ',
                  filters=['nigger', 'nigga', 'faggot', 'cuck', 'retard']),

              # Filter sexually inapproriate usernames.
              StringFilterValidator(
                  prefix='Sexually inapproriate username. ',
                  filters=['anal', 'cock', 'vore', 'scat', 'fuck', 'pussy',
                           'penis', 'piss', 'shit']),

              # Filter likely user bots.
              StringFilterValidator(
                  prefix='Likely user bot. ',
                  filters=['discord\.gg', 'twitter\.com', 'twitch\.tv',
                           'youtube\.com', 'youtu\.be',
                           '@everyone', '@here']))

def _get_validation_config(ctx):
    return ctx.session.query(models.GuildValidationConfig).get(ctx.guild.id)

def _get_rejection_reasons(bot, member):
    for validator in VALIDATORS:
        try:
            yield from validator.get_rejection_reasons(bot, member)
        except:
            # TODO(james7132) Handle the error
            traceback.print_exc()

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
        self.purge_unverified.start()
        self.reload_bans.start()

    def cog_unload(self):
        self.purge_unverified.cancel()
        self.reload_bans.cancel()

    @tasks.loop(minutes=5)
    async def reload_bans(self):
        print('RELOADING BANS')
        await self._update_ban_list()

    @reload_bans.before_loop
    async def before_reload_bans(self):
        await self.bot.wait_until_ready()

    @tasks.loop(seconds=5.0)
    async def purge_unverified(self):
        session = self.bot.create_db_session()
        configs = session.query(models.GuildValidationConfig) \
                        .filter_by(is_propogated=True) \
                        .all()
        guilds = ((conf, self.bot.get_guild(conf.guild_id)) for conf in configs)
        check_time = datetime.utcnow() - PURGE_LOOKBACK
        def _is_kickable(member):
            # Does not kick
            #  * Bots
            #  * Nitro Boosters
            #  * Verified users
            #  * Unverified users who have joined less than 6 hours ago.
            checks = (not member.bot,
                      member.premium_since is None,
                      member.joined_at is not None,
                      member.joined_at <= check_time)
            return all(checks)
        async def _kick_member(member):
            try:
                await utils.send_dm(member, PURGE_DM.format(member.guild.name))
            except:
                pass
            await member.kick(reason='Unverified in sufficient time.')
            self.bot.logger.info('Purged {} from {} for not being verified in time.'.format(
                  utils.pretty_print(member), utils.pretty_print(member.guild)))
        tasks = list()
        for conf, guild in guilds:
            role = guild.get_role(conf.validation_role_id)
            if role is None or not guild.me.guild_permissions.kick_members:
                continue
            if not guild.chunked:
                await self.bot.request_offline_members(guild)
            unvalidated_members = utils.all_without_roles(guild.members, (role,))
            kickable_members = filter(_is_kickable, unvalidated_members)
            tasks.extend(_kick_member(member) for member in kickable_members)
        await asyncio.gather(*tasks)

    @purge_unverified.before_loop
    async def before_purge_unverified(self):
        await self.bot.wait_until_ready()

    async def _update_ban_list(self):
        session = self.bot.create_db_session()
        async def _get_bans(guild):
            if not guild.me.guild_permissions.ban_members:
                return list()
            try:
                return [models.Ban(guild_id=guild.id, user_id=b.user.id,
                                            reason=b.reason)
                                for b in await guild.bans()]
            except discord.Forbidden as e:
                print('Failed to fetch {}\'s bans'.format(guild.name))
                return list()
        bans = await asyncio.gather(*[_get_bans(g) for g in self.bot.guilds])
        session.query(models.Ban).delete()
        for ban_list in bans:
            session.add_all(ban_list)
        session.commit()
        self.bot.logger.info('Updated ban list')

    @commands.command(name="setmodlog")
    @commands.guild_only()
    async def setmodlog(self, ctx, channel: discord.TextChannel=None):
        # TODO(jame7132): Update this so it's in a different cog.
        channel = channel or ctx.channel
        proxy = ctx.get_guild_proxy()
        proxy.set_modlog_channel(channel)
        proxy.save()
        ctx.session.commit()
        await ctx.send(":thumbsup: Set {}'s modlog to {}.".format(
            ctx.guild.name, channel.mention))

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
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
        await ctx.send('Validation configuration complete! Please run `~validation propagate` then `~validation lockdown` to complete setup.')

    @validation.command(name="propagate")
    @commands.bot_has_permissions(manage_roles=True)
    async def validation_propagate(self, ctx):
        config = _get_validation_config(ctx)
        if config is None:
            await ctx.send('No validation config was found. Please run `~valdiation setup`')
            return
        msg = await ctx.send('Propagating validation role...!')
        if not ctx.guild.chunked:
            await ctx.bot.request_offline_members(ctx.guild)
        role = ctx.guild.get_role(config.validation_role_id)
        if role is None:
            await ctx.send("Verification role not found.")
            config.is_propogated = False
            session.add(config)
            session.commit()
            return
        while True:
            filtered_members = [m for m in guild.members if role not in m.roles]
            member_count = len(filtered_members)
            total_processed = 0
            async def add_role(member, role):
                if role in member.roles:
                    return
                try:
                    reasons = list(_get_validation_reasons(member))
                    if len(reasons) < 0:
                        await member.add_roles(role)
                except discord.errors.Forbidden:
                    pass
            for chunk in _chunk_iter(ctx.guild.members, BATCH_SIZE):
                await asyncio.gather(*[add_role(mem, role) for mem in chunk])
                total_processed += len(chunk)
                await msg.edit(content=f'Propagation Ongoing ({total_processed}/{member_count})...')
            await msg.edit(content=f'Propagation conplete!')

            members_with_role = [m for m in guild.members if role in m.roles]
            if float(len(members_with_role)) / float(member_count) > 0.99:
                config.is_propogated = True
                session.add(config)
                session.commit()
                return


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
        proxy = proxies.GuildProxy(member.guild, session)
        if not proxy.validation_config.is_valid:
            return
        reasons = list(_get_rejection_reasons(self.bot, member))
        if len(reasons) > 0:
            response = (f"{utils.mention_random_online_mod(member.guild)}. "
                        f"User {member.name} ({member.id}) requires manual verification. \n"
                        f"Rejected for the following reasons: "
                        f"\n{format.bullet_list(reasons)}")
            await proxy.send_modlog_message(content=response)
            # print('{} ({}) rejected!'.format(member.name.encode('utf-8'), member.id))
            # for reason in reasons:
                # print('  Rejected for: {}'.format(reason))
            return
        role = member.guild.get_role(proxy.validation_config.validation_role_id)
        await member.add_roles(role)
        await proxy.send_modlog_message(content="Verified user: {} ({}).".format(
            member.mention, member.id))
        print('{} ({}) verified!'.format(member.name.encode('utf-8'), member.id))

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        session = self.bot.create_db_session()
        ban = models.Ban(guild_id=guild.id, user_id=user.id)
        try:
            ban_info = await guild.fetch_ban(user)
            ban.reason = ban_info.reason
        finally:
            session.add(ban)
            session.commit()

        if guild.member_count >= MINIMUM_GUILD_SIZE:
            # TODO(james7132): Enable this after adding deduplication.
            # await self.report_bans(ban)
            pass

    async def report_bans(self, ban_info):
        session = self.bot.create_db_session()
        guild_proxies = []
        for guild in self.bot.guilds:
            member = guild.get_member(user.id)
            if member is not None:
                guild_proxies.append(proxies.GuildProxy(guild, session))

        contents = None
        if ban_info.reason is None:
            contents = ("User {} ({}) has been banned from another server.".format(
                user.mention, user.id))
        else:
            contents = ("User {} ({}) has been banned from another server for "
                        "the following reason: `{}`").format(
                            user.mention, user.id, ban_info.reason)

        await asyncio.gather(*[proxy.send_modlog_message(contents)
                               for proxy in guild_proxies])

def setup(bot):
    bot.add_cog(Validation(bot))
