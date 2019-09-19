from .common import Validator

class NitroApprover(Validator):
    """A suspicion level validator that approves users that have Nitro.

    Note: there is currently no way to confirm if a user has Nitro or not
    directly from the API. The only way to confirm it is through peripheral
    effects (i.e. animated avatars), so this validator has a higher chance of
    false negatives.

    Uses the following attributes only Nitro users have access to:
     - Nitro Boosting: only works on servers shared between the bot and the user.
     - Animated Avatars
     - Third-Party Emotes (not implemented)
    """

    async def get_approval_reasons(self, bot, member):
        if member.is_avatar_animated() or _is_nitro_booster(bot, member):
            yield 'User has Nitro. Probably not a user bot.'

    def _is_nitro_booster(self, bot, member):
        """Checks if the user is boosting any server the bot is on."""
        return any(m.id == member.id
                   for g in bot.guilds
                   for m in g.premium_subscribers)

class BotApprover(Validator):
    """A override level validator that approves other bots."""

    async def get_approval_reasons(self, bot, member):
        if member.bot:
            yield 'User is an OAuth2 bot that can only be manually added.'

class BotOwnerApprover(Validator):
    """An override level validator that approves the owner of the bot."""

    async def get_approval_reasons(self, bot, member):
        app_info = await bot.application_info()
        if member.owner == member:
            yield "User owns this bot."

class BotTeamApprover(Validator):
    """An override level validator that approves members of the Discord Team that
    manages the bot."""

    async def get_approval_reasons(self, bot, member):
        app_info = await bot.application_info()
        if bot.team is None:
            return
        if any(team_member.id == member.id for team_member in bot.team.members):
            yield "User is on the team that manages this bot."
