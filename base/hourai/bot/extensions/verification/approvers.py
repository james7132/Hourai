from .common import Verifier
from hourai import utils


class NitroApprover(Verifier):
    """A suspicion level verifier that approves users that have Nitro.

    Note: there is currently no way to confirm if a user has Nitro or not
    directly from the API. The only way to confirm it is through peripheral
    effects (i.e. animated avatars), so this verifier has a higher chance of
    false negatives.

    Uses the following attributes only Nitro users have access to:
     - Nitro Boosting: only works on servers shared between the bot and the
       user.
     - Animated Avatars
     - Third-Party Emotes (not implemented)
    """

    async def verify_member(self, ctx):
        if utils.has_nitro(ctx.bot, ctx.member):
            ctx.add_approval_reason('User currently has or has had Nitro. '
                                    'Probably not a user bot.')


class BotApprover(Verifier):
    """A override level verifier that approves other bots."""

    async def verify_member(self, ctx):
        if ctx.member.bot:
            ctx.add_approval_reason(
                'User is an OAuth2 bot that can only be manually added.')


class BotOwnerApprover(Verifier):
    """An override level verifier that approves the owner of the bot or part of
    the team that owns the bot."""

    async def verify_member(self, ctx):
        if (await ctx.bot.is_owner(ctx.member)):
            ctx.add_approval_reason("User owns this bot.")


class DistinguishedUserApprover(Verifier):
    """A malice level approver that approves distinguished users. Approves the
    following:
    - Discord Staff
    - Discord Partners
    - Owners of verified servers*

    Since bot users cannot read profile data, the bot must be on the same
    server for this to work. Any of the designations marked above by a * may
    result in many false negatives.
    """

    FLAG_MATCHES = {
        "staff": 'User is Discord Staff.',
        "partner": 'User is a Discord Partner.',
        "verified_bot_developer": 'User is a verified bot developer.',
    }

    SERVER_SEARCH_MATCHES = {
        "VERIFIED": 'User is owner of verfied server: "{}"'
    }

    async def verify_member(self, ctx):
        self.__verify_via_user_flags(ctx)
        self.__verify_via_server_ownership(ctx)

    def __verify_via_user_flags(self, ctx):
        flags = ctx.member.public_flags
        for attr, reason in self.FLAG_MATCHES.items():
            if getattr(flags, attr):
                ctx.add_approval_reason(reason)

    def __verify_via_server_ownership(self, ctx):
        # FIXME: This will not scale to multiple processes/nodes
        owned_guilds = [guild for guild in ctx.bot.guilds
                        if guild.owner == ctx.member]
        for guild in owned_guilds:
            for check, reason_template in self.SERVER_SEARCH_MATCHES.items():
                if check in guild.features:
                    ctx.add_approval_reason(reason_template.format(guild.name))
