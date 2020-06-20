from .common import Validator
from hourai import utils


class NitroApprover(Validator):
    """A suspicion level validator that approves users that have Nitro.

    Note: there is currently no way to confirm if a user has Nitro or not
    directly from the API. The only way to confirm it is through peripheral
    effects (i.e. animated avatars), so this validator has a higher chance of
    false negatives.

    Uses the following attributes only Nitro users have access to:
     - Nitro Boosting: only works on servers shared between the bot and the
       user.
     - Animated Avatars
     - Third-Party Emotes (not implemented)
    """

    async def validate_member(self, ctx):
        if utils.has_nitro(ctx.bot, ctx.member):
            ctx.add_approval_reason('User has Nitro. Probably not a user bot.')


class BotApprover(Validator):
    """A override level validator that approves other bots."""

    async def validate_member(self, ctx):
        if ctx.member.bot:
            ctx.add_approval_reason(
                'User is an OAuth2 bot that can only be manually added.')


class BotOwnerApprover(Validator):
    """An override level validator that approves the owner of the bot or part of
    the team that owns the bot."""

    async def validate_member(self, ctx):
        if (await ctx.bot.is_owner(ctx.member)):
            ctx.add_approval_reason("User owns this bot.")


class DistinguishedGuildOwnerApprover(Validator):
    """A malice level approver that approves the owners of "distinguished"
    servers (i.e. Partnered or Verified servers).

    Since bot users cannot read profile data, the bot must be on the same
    distinguished server for this to work. As a result there may be many false
    negatives.
    """

    MATCHES = {
        "PARTNERED": 'User is owner of partnered server: "{}"',
        "VERIFIED": 'User is owner of verfied server: "{}"'
    }

    async def validate_member(self, ctx):
        owned_guilds = [guild for guild in ctx.bot.guilds
                        if guild.owner == ctx.member]
        for guild in owned_guilds:
            features = set(guild.features)
            for check, reason_template in self.MATCHES.items():
                if check in features:
                    ctx.add_approval_reason(reason_template.format(guild.name))
