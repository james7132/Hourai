import asyncio
import re
from discord.ext import commands
from hourai.bot import GuildSpecificCog

_BIG_SERVER_SIZE = 250
_DISCORD_INVITE_REGEX = re.compile('discord.gg/([a-zA-Z0-9]+)')

class GuildSpecific_TheGap(GuildSpecificCog):

    async def _is_server_big(self, invites):
        async def _is_big_invite(inv):
            if inv is None:
                return False
            try:
                invite = await self.bot.fetch_invite(inv)
                return invite.approximate_member_count >= _BIG_SERVER_SIZE
            except:
                logging.exception("Failed to check big server.")
                return False
        return any(await asyncio.gather(*[_is_big_invite(inv)
                                          for inv in invites]))

    @commands.Cog.listener()
    async def on_message(self, msg):
        # Deletes any message that doesn't contain a server link.
        category = msg.channel.category
        if category is None or 'server' not in category.name.lower():
            return
        matches = _DISCORD_INVITE_REGEX.findall(msg.content)
        delete = len(matches) <= 0
        if not delete and 'big' in msg.channel.name:
            delete |= not (await self._is_server_big(matches))
        if delete:
            await msg.delete()

__GUILD_COGS = {
    GuildSpecific_TheGap: {355145270029451264},
}

def setup(bot):
    for cls, guilds in __GUILD_COGS.items():
        bot.add_cog(cls(bot, guilds=set(guilds)))
