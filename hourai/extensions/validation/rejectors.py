import humanize
import re
from datetime import datetime
from hourai import utils
from .common import Validator, generalize_filter, split_camel_case
from .storage import BanStorage


LOOSE_DELETED_USERNAME_MATCH = re.compile(r'(?i).*Deleted.*User.*')


class NameMatchRejector(Validator):
    """A suspicion level validator that rejects users for username proximity to
    other users already on the server.
    """

    def __init__(self, *, prefix, filter_func,
                 min_match_length=None, subfield=None, member_selector=None):
        self.filter = filter_func
        self.prefix = prefix
        self.subfield = subfield or (lambda m: m.name)
        self.member_selector = member_selector or (lambda m: m.name)
        self.min_match_length = min_match_length

    async def validate_member(self, ctx):
        member_names = {}
        for guild_member in filter(self.filter, ctx.guild.members):
            name = self.member_selector(guild_member) or ''
            member_names.update({
                p: generalize_filter(p) for p in self._split_name(name)
            })
        field_value = self.subfield(ctx.member)
        for filter_name, regex in member_names.items():
            if re.search(regex, field_value):
                ctx.add_rejection_reason(
                    self.prefix + f'Matches: `{filter_name}`')

    def _split_name(self, name):
        split_name = split_camel_case(name)
        if self.min_match_length is not None:
            split_name = (n for n in split_name
                          if len(n) >= self.min_match_length)
        return split_name


class StringFilterRejector(Validator):
    """A general validator that rejects users that have a field that matches
    a set of predefined list of regexes.
    """

    def __init__(self, *, prefix, filters, full_match=False, subfield=None):
        self.prefix = prefix or ''
        self.filters = [(f, re.compile(generalize_filter(f))) for f in filters]
        self.subfield = subfield or (
            lambda ctx: (u.name for u in ctx.usernames))
        if full_match:
            self.match_func = lambda r: r.match
        else:
            self.match_func = lambda r: r.search
        print(self.filters)

    async def validate_member(self, ctx):
        for field_value in self.subfield(ctx):
            for filter_name, regex in self.filters:
                if self.match_func(regex)(field_value):
                    ctx.add_rejection_reason(
                        self.prefix + f'Matches: `{filter_name}`')


class NewAccountRejector(Validator):
    """A suspicion level validator that rejects users that were recently
    created.
    """

    def __init__(self, *, lookback):
        self.lookback = lookback

    async def validate_member(self, ctx):
        if ctx.member.created_at > datetime.utcnow() - self.lookback:
            lookback_naturalized = humanize.naturaltime(self.lookback)
            ctx.add_rejection_reason(
                f"Account created less than {lookback_naturalized}")


class DeletedAccountRejector(Validator):
    """A suspicion level validator that rejects users that are deleted or have
    tell-tale warning signs of faking a deleted account in the past.
    """

    async def validate_member(self, ctx):
        if utils.is_deleted_user(ctx.member):
            ctx.add_rejection_reason(
                "Deleted users cannot be active on Discord. User has been "
                "deleted by Discord of their own accord or for Trust and "
                "Safety reasons, or is faking account deletion.")

        for username in ctx.usernames:
            is_deleted = utils.is_deleted_username(username.name)
            if LOOSE_DELETED_USERNAME_MATCH.match(username.name) and \
               not is_deleted:
                ctx.add_rejection_reason(
                    f'"{username.name}" does not match Discord\'s deletion '
                    f'patterns. User may have attempted to fake account '
                    f"deletion.")
            elif is_deleted and username.discriminator is not None and \
                    username.discriminator < 100:
                ctx.add_rejection_reason(
                    f'"{username.name}#{username.discriminator}" has an '
                    f'unusual discriminator. These are randomly generated. '
                    f'User may have attempted to fake account deletion.')


class NoAvatarRejector(Validator):
    """A suspicion level validator that rejects users without avatars."""

    async def validate_member(self, ctx):
        if ctx.member.avatar is None:
            ctx.add_rejection_reason("User has no avatar.")


class BannedUserRejector(Validator):
    """A malice level validator that rejects users that are banned on other
    servers.
    """

    def __init__(self, *, min_guild_size):
        self.min_guild_size = min_guild_size

    async def validate_member(self, ctx):
        banned = False
        reasons = set()
        guild_ids = [g.id for g in ctx.bot.guilds if self._is_valid_guild(g)]
        bans = await BanStorage(ctx.bot).get_bans(ctx.member.id, guild_ids)
        for ban in bans:
            guild = ctx.bot.get_guild(ban.guild_id)
            assert self._is_valid_guild(guild)
            if ban.reason is not None and ban.reason not in reasons:
                ctx.add_rejection_reason(
                    f"Banned on another server. Reason: `{ban.reason}`.")
                reasons.add(ban.reason)
            banned = True

        if len(reasons) == 0 and banned:
            ctx.add_rejection_reason("Banned on another server.")

    def _is_valid_guild(self, guild):
        return guild is not None and guild.member_count >= self.min_guild_size


class BannedUserRejector(Validator):
    """A malice level validator that rejects users that share characteristics
    with banned users on the server:
     - Exact username matches (ignoring repeated whitespace and casing).
     - Exact avatar matches.
    """

    async def validate_member(self, ctx):
        if not ctx.guild.me.guild_permissions.ban_members:
            return
        # TODO(james7132): Make this use the Ban cache to pull this information
        bans = await ctx.guild.bans()
        self.__check_usernames(ctx, bans)
        self.__check_avatars(ctx, bans)

    def __check_avatars(self, ctx, bans):
        avatar = ctx.member.avatar
        if avatar is None:
            return
        for ban in bans:
            if avatar != ban.user.avatar:
                continue
            ctx.add_rejection_reason(
                f"Exact avatar match with banned user: `{str(ban.user)}`.")

    def __check_usernames(self, ctx, bans):
        normalized_bans = {self._normalize(ban.user.name): ban.user.name
                           for ban in bans}
        for username in ctx.usernames:
            name = self._normalize(username.name)
            for normalized, original in normalized_bans.items():
                if name != normalized:
                    continue
                ctx.add_rejection_reason(
                    f"Exact username match with banned user: `{original}`.")
                break

    def _normalize(self, val):
        return " ".join(val.casefold().split())


class LockdownRejector(Validator):
    """A malice level validator that rejects all users if the guild has been put
    into lockdown.
    """

    async def validate_member(self, ctx):
        guild_state = ctx.bot.guild_states[ctx.guild.id]
        if guild_state.is_locked_down:
            ctx.add_rejection_reason(
                'Lockdown enabled. All new joins must be manually verified.')
