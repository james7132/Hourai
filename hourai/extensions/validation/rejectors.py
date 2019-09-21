import humanize
import re
from datetime import datetime
from hourai import utils
from .common import Validator, generalize_filter, split_camel_case
from .storage import BanStorage

class NameMatchRejector(Validator):
    """A suspicion level validator that rejects users for username proximity to
    other users already on the server.
    """

    def __init__(self, *, prefix, filter_func, subfield=None, member_selector=None):
        self.filter = filter_func
        self.subfield = subfield or (lambda m: m.name)
        self.member_selector = member_selector or (lambda m: m.name)

    async def get_rejection_reasons(self, bot, member):
        member_names = {}
        for guild_member in filter(self.filter, member.guild.members):
            name = self.member_selector(guild_member) or ''
            member_names.update({
                p: generalize_filter(p) for p in split_camel_case(name)
            })
        field_value = self.subfield(member)
        for filter_name, regex in member_names.items():
            if re.search(regex, field_value):
                yield prefix + 'Matches: `{}`'.format(filter_name)

class StringFilterRejector(Validator):
    """A general validator that rejects users that have a field that matches
    a set of predefined list of regexes.
    """

    def __init__(self, *, prefix, filters, full_match=False, subfield=None):
        self.prefix = prefix or ''
        self.filters = [(f, re.compile(generalize_filter(f))) for f in filters]
        self.subfield = subfield or (lambda m: m.name)
        if full_match:
            self.match_func = lambda r: r.match
        else:
            self.match_func = lambda r: r.search
        print(self.filters)

    async def get_rejection_reasons(self, bot, member):
        field_value = self.subfield(member)
        for filter_name, regex in self.filters:
            if self.match_func(regex)(field_value):
                yield self.prefix + 'Matches: `{}`'.format(filter_name)

class NewAccountRejector(Validator):
    """A suspicion level validator that rejects users that were recently
    created.
    """

    def __init__(self, *, lookback):
        self.lookback = lookback

    async def get_rejection_reasons(self, bot, member):
        if member.created_at > datetime.utcnow() - self.lookback:
            yield "Account created less than {}".format(humanize.naturaltime(self.lookback))

class DeletedAccountRejector(Validator):
    """A suspicion level validator that rejects users that are deleted."""

    async def get_rejection_reasons(self, bot, member):
        if utils.is_deleted_user(member):
            yield ("User has been deleted by Discord at their own consent or for"
            " Trust and Safety reasons, or is faking account deletion.")

class NoAvatarRejector(Validator):
    """A suspicion level validator that rejects users without avatars."""

    async def get_rejection_reasons(self, bot, member):
        if member.avatar is None:
            yield "User has no avatar."

class BannedUserRejector(Validator):
    """A malice level validator that rejects users that are banned on other
    servers.
    """

    def __init__(self, *, min_guild_size):
        self.min_guild_size = min_guild_size

    async def get_rejection_reasons(self, bot, member):
        banned = False
        reasons = set()
        guild_ids = [g.id for g in bot.guilds if self._is_valid_guild(g)]
        bans = await BanStorage(bot).get_bans(member.id, guild_ids)
        for ban in bans:
            guild = bot.get_guild(ban.guild_id)
            assert self._is_valid_guild(guild)
            if ban.reason is not None and ban.reason not in reasons:
                yield "Banned on another server. Reason: `{}`.".format(r)
                reasons.add(ban.reason)
            banned = True

        if len(reasons) == 0 and banned:
            yield "Banned on another server."

    def _is_valid_guild(self, guild):
        return guild is not None and guild.member_count >= self.min_guild_size
