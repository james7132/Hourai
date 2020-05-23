import asyncio
import discord
import logging
from . import approvers, rejectors
from .storage import BanStorage
from .context import ValidationContext
from discord.ext import tasks, commands
from datetime import datetime, timedelta
from hourai import utils
from hourai.cogs import BaseCog
from hourai.db import proxies, proto
from hourai.utils import format, checks

log = logging.getLogger(__name__)

PURGE_LOOKBACK = timedelta(hours=6)
PURGE_DM = ("You have been kicked from {} due to not being verified within "
            "sufficient time.  If you feel this is in error, please contact a "
            "mod regarding this.")
BATCH_SIZE = 10
MINIMUM_GUILD_SIZE = 150

APPROVE_REACTION = '\u2705'
KICK_REACTION = '\u274C'
BAN_REACTION = '\u2620'
MODLOG_REACTIONS = (APPROVE_REACTION, KICK_REACTION, BAN_REACTION)

# TODO(james7132): Add per-server validation configuration.
# TODO(james7132): Add filter for pornographic or violent avatars
# Validators are applied in order from first to last. If a later validator has
# an approval reason, it overrides all previous rejection reasons.
VALIDATORS = (
    # ---------------------------------------------------------------
    # Suspicion Level Validators
    #     Validators here are mostly for suspicious characteristics.
    #     These are designed with a high-recall, low precision
    #     methdology. False positives from these are more likely.
    #     These are low severity checks.
    # -----------------------------------------------------------------

    # New user accounts are commonly used for alts of banned users.
    rejectors.NewAccountRejector(lookback=timedelta(days=30)),
    # Low effort user bots and alt accounts tend not to set an avatar.
    rejectors.NoAvatarRejector(),
    # Deleted accounts shouldn't be able to join new servers. A user
    # joining that is seemingly deleted is suspicious.
    rejectors.DeletedAccountRejector(),

    # Filter likely user bots based on usernames.
    rejectors.StringFilterRejector(
        prefix='Likely user bot. ',
        filters=[r'discord\.gg', r'twitter\.com', r'twitch\.tv',
                 r'youtube\.com', r'youtu\.be',
                 '@everyone', '@here', 'admin', 'mod']),
    rejectors.StringFilterRejector(
        prefix='Likely user bot. ',
        full_match=True,
        filters=['[0-9a-fA-F]+',  # Full Hexadecimal name
                 r'\d+',          # Full Decimal name
                 ]),

    # If a user has Nitro, they probably aren't an alt or user bot.
    approvers.NitroApprover(),

    # -----------------------------------------------------------------
    # Questionable Level Validators
    #     Validators here are mostly for red flags of unruly or
    #     potentially troublesome.  These are designed with a
    #     high-recall, high-precision methdology. False positives from
    #     these are more likely to occur.
    # -----------------------------------------------------------------

    # Filter usernames and nicknames that match moderator users.
    rejectors.NameMatchRejector(
        prefix='Username matches moderator\'s. ',
        filter_func=utils.is_moderator,
        min_match_length=4),
    rejectors.NameMatchRejector(
        prefix='Username matches moderator\'s. ',
        filter_func=utils.is_moderator,
        member_selector=lambda m: m.nick,
        min_match_length=4),

    # Filter usernames and nicknames that match bot users.
    rejectors.NameMatchRejector(
        prefix='Username matches bot\'s. ',
        filter_func=lambda m: m.bot,
        min_match_length=4),
    rejectors.NameMatchRejector(
        prefix='Username matches bot\'s. ',
        filter_func=lambda m: m.bot,
        member_selector=lambda m: m.nick,
        min_match_length=4),

    # Filter offensive usernames.
    rejectors.StringFilterRejector(
        prefix='Offensive username. ',
        filters=['nigger', 'nigga', 'faggot', 'cuck', 'retard']),

    # Filter sexually inapproriate usernames.
    rejectors.StringFilterRejector(
        prefix='Sexually inapproriate username. ',
        filters=['anal', 'cock', 'vore', 'scat', 'fuck', 'pussy', 'urethra',
                 'rape', 'penis', 'piss', 'shit', 'cum']),

    # -----------------------------------------------------------------
    # Malicious Level Validators
    #     Validators here are mostly for known offenders.
    #     These are designed with a low-recall, high precision
    #     methdology. False positives from these are far less likely to
    #     occur.
    # -----------------------------------------------------------------

    # Make sure the user is not banned on other servers.
    rejectors.BannedUserRejector(min_guild_size=150),

    # Check the username against known banned users from the current
    # server. Requires exact username match (case insensitive)
    rejectors.BannedUsernameRejector(),

    # Check if the user owns a Partnered or Verified server, and approve them.
    approvers.DistinguishedGuildOwnerApprover(),

    # All non-override users are rejected while guilds are locked down.
    rejectors.LockdownRejector(),

    # -----------------------------------------------------------------
    # Override Level Validators
    #     Validators here are made to explictly override previous
    #     validators. These are specifically targetted at a small
    #     specific group of individiuals. False positives and negatives
    #     at this level are very unlikely if not impossible.
    # -----------------------------------------------------------------
    approvers.BotApprover(),
    approvers.BotOwnerApprover(),
)


def _chunk_iter(src, chunk_size):
    chunk = []
    for val in src:
        chunk.append(val)
        if len(chunk) >= chunk_size:
            yield chunk
            chunk = []
    yield chunk


class Validation(BaseCog):

    def __init__(self, bot):
        super().__init__()
        self.bot = bot
        self.ban_storage = BanStorage(bot, timeout=300)
        # self.purge_unverified.start()
        self.reload_bans.start()

    def cog_unload(self):
        # self.purge_unverified.cancel()
        self.reload_bans.cancel()

    @tasks.loop(seconds=180)
    async def reload_bans(self):
        for guild in self.bot.guilds:
            try:
                await self.ban_storage.save_bans(guild)
            except Exception:
                log.exception(
                    f"Exception while reloading bans for guild {guild.id}:")

    @reload_bans.before_loop
    async def before_reload_bans(self):
        await self.bot.wait_until_ready()

    @commands.Cog.listener()
    async def on_member_ban(self, guild, user):
        await self.bot.wait_util_ready()
        if not guild.me.guild_permissions.ban_members:
            return
        try:
            ban_info = await guild.fetch_ban(user)
            await self.ban_storage.save_ban(guild.id, ban_info.user.id,
                                            ban_info.reason)
        except discord.Forbidden:
            pass

        if guild.member_count >= MINIMUM_GUILD_SIZE:
            # TODO(james7132): Enable this after adding deduplication.
            # await self.report_bans(ban)
            pass

    @commands.Cog.listener()
    async def on_member_unban(self, guild, user):
        await self.ban_storage.clear_ban(guild, user)

    @tasks.loop(seconds=5.0)
    async def purge_unverified(self):
        check_time = datetime.utcnow()

        def _is_kickable(member, lookback_time):
            # Does not kick
            #  * Bots
            #  * Nitro Boosters
            #  * Verified users
            #  * Unverified users who have joined less than 6 hours ago.
            checks = (not member.bot,
                      member.joined_at is not None,
                      member.joined_at <= lookback_time)
            return all(checks)

        async def _kick_member(member):
            try:
                await utils.send_dm(member, PURGE_DM.format(member.guild.name))
            except Exception:
                pass
            await member.kick(reason='Unverified in sufficient time.')
            mem = utils.pretty_print(member)
            gld = utils.pretty_print(member.guild)
            log.info(
                f'Purged {mem} from {gld} for not being verified in time.')

        async def _purge_guild(proxy):
            guild = proxy.guild
            validation_config = await proxy.get_config('validation')
            if (validation_config is None or
                not validation_config.enabled or
                not validation_config.HasField(
                    'kick_unvalidated_users_after')):
                return
            lookback = timedelta(
                seconds=validation_config.kick_unvalidated_users_after)
            lookback = check_time - lookback
            role = guild.get_role(validation_config.role_id)
            if role is None or not guild.me.guild_permissions.kick_members:
                return
            if not guild.chunked:
                await self.bot.request_offline_members(guild)
            unvalidated_members = utils.all_without_roles(
                guild.members, (role,))
            kickable_members = filter(lambda m: _is_kickable(m, lookback),
                                      unvalidated_members)
            tasks = [_kick_member(member) for member in kickable_members]
            await asyncio.gather(*tasks)

        guild_proxies = [proxies.GuildProxy(self.bot, guild)
                         for guild in self.bot.guilds]
        await asyncio.gather(*[_purge_guild(proxy) for proxy in guild_proxies])

    @purge_unverified.before_loop
    async def before_purge_unverified(self):
        await self.bot.wait_until_ready()

    @commands.command(name="setmodlog")
    @checks.is_moderator()
    @commands.guild_only()
    async def setmodlog(self, ctx, channel: discord.TextChannel = None):
        # TODO(jame7132): Update this so it's in a different cog.
        channel = channel or ctx.channel
        await ctx.guild_proxy.set_modlog_channel(channel)
        await ctx.send(":thumbsup: Set {}'s modlog to {}.".format(
            ctx.guild.name,
            channel.mention if channel is not None else 'None'))

    @commands.command(name="getbans")
    @commands.is_owner()
    async def getbans(self, ctx, user_id: int):
        guild_ids = (g.id for g in ctx.bot.guilds)
        bans = await self.ban_storage.get_bans(user_id, guild_ids)
        bans = (f'{ban.guild_id}: {ban.reason}' for ban in bans)
        await ctx.send(format.vertical_list(bans))

    @commands.group(invoke_without_command=True)
    @commands.guild_only()
    @commands.check_any(checks.is_moderator(), commands.is_owner())
    async def validation(self, ctx):
        pass

    @validation.group(name='lockdown')
    async def validation_lockdown(self, ctx, time: utils.human_timedelta):
        """Manually locks down the server. Forces manual verification of almost
        all users joining the server for the duration. This isn't useful unless
        validation is enabled. See "~help validation setup" for more
        information. A lockdown can be lifted via "~valdiation lockdown lift".

        Warning: this is currently non-persistent. If the bot is restarted
        while a lockdown is in place, it will be lifted without warning.

        Example Usage:
        ~validation lockdown 30m
        ~validation lockdown 1h
        ~validation lockdown 1d
        """
        expiration = datetime.utcnow() + time
        guild_state = ctx.bot.guild_states[ctx.guild.id]
        guild_state.set_lockdown(True, expiration=expiration)
        await ctx.send(
            f'Lockdown enabled. Will be automatically lifted at {expiration}')

    @validation_lockdown.command(name='lockdown')
    async def validation_lockdown_lift(self, ctx, time: utils.human_timedelta):
        """Lifts a lockdown from the server. See "~help validation lockdown" for
        more information.

        Warning: this is currently non-persistent. If the bot is restarted
        while a lockdown is in place, it will be lifted without warning.

        Example Usage:
        ~validation lockdown lift
        """
        guild_state = ctx.bot.guild_states[ctx.guild.id]
        guild_state.set_lockdown(False)
        await ctx.send('Lockdown disabled.')

    @validation.command(name='verify')
    async def validation_verify(self, ctx, member: discord.Member):
        """Runs verification on a provided user.
        Must be a moderator or owner of the server to use this command.
        Validation must be enabled on a server before running this. See
        "~help validation setup" for more information.

        Example Usage:
        ~validation verify @Bob
        ~validation verify Alice
        """
        config = await proxies.GuildProxy(self.bot, member.guild) \
            .get_config('validation')
        if config is None or not config.enabled:
            await ctx.send('Validation has not been setup. Please see '
                           '`~help validation` for more details.')
            return

        validation_ctx = ValidationContext(ctx.bot, member, config)
        await validation_ctx.validate_member(VALIDATORS)
        await validation_ctx.send_log_message(ctx)

    @validation.command(name="setup")
    async def validation_setup(self, ctx, role: discord.Role = None):
        config = await ctx.bot.storage.validation_configs.get(ctx.guild.id)
        config = config or proto.ValidationConfig()
        config.enabled = True
        if role is not None:
            config.role_id = role.id
        await ctx.bot.storage.validation_configs.set(ctx.guild.id, config)
        await ctx.send('Validation configuration complete! Please run '
                       '`~validation propagate` to'
                       ' complete setup.')

    @validation.command(name="propagate")
    @commands.bot_has_permissions(manage_roles=True)
    async def validation_propagate(self, ctx):
        config = await ctx.bot.storage.validation_configs.get(ctx.guild.id)
        if config is None or not config.HasField('role_id'):
            await ctx.send('No validation config was found. Please run '
                           '`~valdiation setup`')
            return
        msg = await ctx.send('Propagating validation role...!')
        if not ctx.guild.chunked:
            await ctx.bot.request_offline_members(ctx.guild)
        role = ctx.guild.get_role(config.role_id)
        if role is None:
            await ctx.send("Verification role not found.")
            config.ClearField('kick_unvalidated_users_after')
            await ctx.bot.storage.validation_configs.set(ctx.guild.id, config)
            return
        while True:
            filtered_members = [m for m in ctx.guild.members
                                if role not in m.roles]
            member_count = len(filtered_members)
            total_processed = 0

            async def add_role(member, role):
                if role in member.roles:
                    return
                try:
                    validation_ctx = ValidationContext(ctx.bot, member, config)
                    await validation_ctx.validate_member(VALIDATORS)
                    await self.verify_member(validation_ctx)
                except (discord.errors.Forbidden, discord.errors.NotFound):
                    # If members leave mid propogation, it 404s
                    pass
            for chunk in _chunk_iter(filtered_members, BATCH_SIZE):
                await asyncio.gather(*[add_role(mem, role) for mem in chunk])
                total_processed += len(chunk)
                progress = f'{total_processed}/{member_count}'
                await msg.edit(content=f'Propagation Ongoing ({progress})...')

            members_with_role = [m for m in ctx.guild.members
                                 if role in m.roles]
            if (member_count == 0 or
                    float(len(members_with_role)) / float(member_count) > 0.99):
                lookback = int(PURGE_LOOKBACK.total_seconds())
                config.kick_unvalidated_users_after = lookback
                await ctx.bot.storage.validation_configs.set(
                    ctx.guild.id, config)
                await msg.edit(content=f'Propagation conplete!')
                return

    @commands.Cog.listener()
    async def on_member_join(self, member):
        config = await proxies.GuildProxy(self.bot, member.guild) \
                              .get_config('validation')
        if config is None or not config.enabled:
            return

        ctx = ValidationContext(self.bot, member, config)
        await ctx.validate_member(VALIDATORS)
        await self.verify_member(ctx)

        msg = await ctx.send_modlog_message()
        try:
            for reaction in MODLOG_REACTIONS:
                await msg.add_reaction(reaction)
        except (AttributeError, discord.errors.Forbidden):
            pass

    @commands.Cog.listener()
    async def on_reaction_add(self, reaction, user):
        msg = reaction.message
        guild = msg.guild
        if (reaction.me or reaction.custom_emoji or guild is None or
                reaction.emoji not in MODLOG_REACTIONS or
                len(msg.embeds) <= 0):
            return
        logging_config = await proxies.GuildProxy(self.bot, guild) \
                                      .get_config('logging')
        if (logging_config is None or
                logging_config.modlog_channel_id != msg.channel.id):
            return
        embed = reaction.message.embed[0]
        try:
            member = guild.get_member(int(embed.footer.text, 16))
            if member is None:
                return
        except ValueError:
            return
        perms = user.guild_permissions
        if reaction.emoji == APPROVE_REACTION and perms.manage_messages:
            config = await self.bot.storage.validation_configs.get(guild.id)
            config = config or proto.ValidationConfig()
            ctx = ValidationContext(self.bot, member, config)
            await self.verify_member(ctx)
        elif reaction.emoji == KICK_REACTION and perms.kick_members:
            await member.kick(reason=(f'Failed verification.'
                                      f' Kicked by {user.name}.'))
        elif reaction.emoji == BAN_REACTION and perms.ban_members:
            await member.ban(reason=(f'Failed verification.'
                                     f' Banned by {user.name}.'))

    async def verify_member(self, ctx):
        await ctx.apply_role()
        self.bot.dispatch('verify_' + ('accept' if ctx.approved else 'reject'),
                          ctx.member)

    async def report_bans(self, ban_info):
        user = ban_info.user
        guild_proxies = [proxies.GuildProxy(self.bot, guild)
                         for guild in self.bot.guilds
                         if guild.get_member(user.id) is not None]

        contents = None
        if ban_info.reason is None:
            contents = (f"User {user.mention} ({user.id}) has been banned "
                        f"from another server.")
        else:
            contents = (f"User {user.mention} ({user.id}) has been banned "
                        f"from another server for the following reason: "
                        f"`{ban_info.reason}`.")

        async def report(proxy):
            modlog = await proxy.get_modlog()
            await modlog.send(contents)

        await asyncio.gather(*[report(proxy) for proxy in guild_proxies])


def setup(bot):
    bot.add_cog(Validation(bot))
