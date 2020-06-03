import asyncio
import re
import sys

__DISCORD_INVITE_REGEX = re.compile('discord.gg/([a-zA-Z0-9]+)')


def is_discord_invite(text):
    """Checks if a string corresponds to a Discord invite link.
    Returns true if it is an invite, false otherwise.
    """
    return __DISCORD_INVITE_REGEX.match(text) is not None


def has_discord_invite(text):
    """Checks if a string contains a Discord invite link.

    Returns true if it contains an invite, false otherwise.
    """
    return __DISCORD_INVITE_REGEX.match(text) is not None


async def get_all_discord_invites(bot, text, on_error=None):
    """Fetches all of the Discord invites from a string.

    Calls the optional on_error parameter with (exception, exception type,
    traceback) if an error occurs. Otherwise it passses the error through.

    Returns a list `discord.Invite` objects.
    """
    matches = __DISCORD_INVITE_REGEX.findall(text)

    async def _fetch_invite(invite):
        try:
            return await bot.fetch_invite(invite)
        except Exception:
            if on_error is not None:
                on_error(*sys.exc_info())
            else:
                raise
    return await asyncio.gather(*[_fetch_invite(match) for match in matches])
