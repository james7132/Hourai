import humanize
import re
from unidecode import unidecode
from datetime import datetime
from hourai import utils
from hourai.db import models
from .common import Validator, generalize_filter, split_camel_case


LOOSE_DELETED_USERNAME_MATCH = re.compile(r'(?i).*Deleted.*User.*')
TRANSFORMS = (lambda x: x, unidecode)


class NameMatchRejector(Validator):
    """A suspicion level validator that rejects users for username proximity to
    other users already on the server.
    """
    __slots__ = ("filter", "prefix", "subfield", "member_selector",
                 "min_match_length")

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
    __slots__ = ("filters", "prefix", "match_func", "subfield", "use_transforms")

    def __init__(self, *, prefix, filters, full_match=False, subfield=None,
                 use_transforms=True):
        self.prefix = prefix or ''
        self.filters = [(f, re.compile(generalize_filter(f))) for f in filters]
        self.use_transforms = use_transforms
        self.subfield = subfield or (
            lambda ctx: (u.name for u in ctx.usernames))
        if full_match:
            self.match_func = lambda r: r.match
        else:
            self.match_func = lambda r: r.search

    async def validate_member(self, ctx):
        transforms = TRANSFORMS if self.use_transforms else (lambda x: x,)
        for field_value in self.subfield(ctx):
            for filter_name, regex in self.filters:
                for transform in transforms:
                    transformed = transform(field_value)
                    if self.match_func(regex)(transformed):
                        ctx.add_rejection_reason(
                            self.prefix + f'Matches: `{filter_name}`')


class NewAccountRejector(Validator):
    """A suspicion level validator that rejects users that were recently
    created.
    """
    __slots__ = ("lookback")

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
    __slots__ = ()

    async def validate_member(self, ctx):
        if utils.is_deleted_user(ctx.member):
            ctx.add_rejection_reason(
                "Deleted users cannot be active on Discord. User has been "
                "deleted by Discord of their own accord or for Trust and "
                "Safety reasons, or is faking account deletion.")

        for transform in TRANSFORMS:
            for username in ctx.usernames:
                name = transform(username.name)
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
    __slots__ = ()

    async def validate_member(self, ctx):
        if ctx.member.avatar is None:
            ctx.add_rejection_reason("User has no avatar.")


class BannedUserRejector(Validator):
    """A malice level validator that rejects users that are banned on other
    servers.
    """
    __slots__ = ("min_guild_size")

    def __init__(self, *, min_guild_size):
        self.min_guild_size = min_guild_size

    async def validate_member(self, ctx):
        bans = await ctx.bot.storage.bans.get_user_bans(ctx.member.id)
        valid_bans = list(filter(self._is_valid_ban, bans))
        if len(valid_bans) <= 0:
            return
        reasons = set(b.reason for b in valid_bans if b.HasField('reason'))
        reason = f"Banned from {len(bans)} servers " if len(bans) > 1 else \
                 "Banned from another server "
        if len(reasons) > 0:
            reason += "for the following reasons:\n"
            reason += "\n    - ".join(reasons)
        ctx.add_rejection_reason(reason)

    def _is_valid_ban(self, ban):
        return not ban.guild_blocked and ban.guild_size >= self.min_guild_size


class BannedUsernameRejector(Validator):
    """A malice level validator that rejects users that share characteristics
    with banned users on the server:
     - Exact username matches (ignoring repeated whitespace and casing).
     - Exact avatar matches.
    """
    __slots__ = ()

    async def validate_member(self, ctx):
        if not ctx.guild.me.guild_permissions.ban_members:
            return
        bans = await ctx.bot.storage.bans.get_guild_bans(ctx.guild.id)
        self.__check_usernames(ctx, bans)
        self.__check_avatars(ctx, bans)

    def __check_avatars(self, ctx, bans):
        avatar = ctx.member.avatar
        if avatar is None:
            return
        for ban in bans:
            if not ban.HasField('avatar') or avatar != ban.avatar:
                continue
            reason = f"Exact username match with banned user: `{str(ban.user)}`."
            if ban.HasField('reason'):
                reason += f" Ban Reason: {ban.reason}"
            ctx.add_rejection_reason(reason)

    def __check_usernames(self, ctx, bans):
        ban_ids = [ban.user_id for ban in bans]
        ban_reasons = {ban.user_id:
                       ban.reason if ban.HasField('reason') else None
                       for ban in bans}
        with ctx.bot.create_storage_session() as session:
            matches = session.query(models.Username) \
                             .filter(models.Username.user_id.in_(ban_ids)) \
                             .distinct(models.Username.name)

            for transform in TRANSFORMS:
                normalized_usernames = set(self._normalize(transform(u.name))
                                           for u in ctx.usernames)
                # Don't match on empty string matches
                normalized_usernames.discard("")

                for banned_username in matches.all():
                    transformed = transform(banned_username.name)
                    normalized = self._normalize(transformed)
                    if not normalized in normalized_usernames:
                        continue
                    ban_reason = ban_reasons.get(banned_username.user_id)
                    reason = f"Exact avatar match with banned user: " + \
                             f"{banned_username.name}`."
                    if ban_reason is not None:
                        reason += f" Ban Reason: {ban_reason}"
                    ctx.add_rejection_reason(reason)
                    break

    def _normalize(self, val):
        return " ".join(val.casefold().split())


class LockdownRejector(Validator):
    """A malice level validator that rejects all users if the guild has been put
    into lockdown.
    """
    __slots__ = ()

    async def validate_member(self, ctx):
        guild_state = ctx.bot.guild_states[ctx.guild.id]
        if guild_state.is_locked_down:
            ctx.add_rejection_reason(
                'Lockdown enabled. All new joins must be manually verified.')
