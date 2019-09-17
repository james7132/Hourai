from .common import Validator

class RaidValidator(Validator):
    """A malice level validator that rejects all users if the guild has been put
    into lockdown.
    """

    async def get_rejection_reasons(self, bot, member):
        if member.avatar is None:
            yield "User has no avatar."

class LockdownValidator(Validator):
    """A malice level validator that rejects all users if the guild has been put
    into lockdown.
    """

    async def get_rejection_reasons(self, bot, member):
        if False:
            yield 'Guild is currently in lockdown. No new joins are approved.'
