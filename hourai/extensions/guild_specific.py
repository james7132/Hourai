# Guild Specific Code

import logging
from hourai.util import invite
from discord.ext import commands
from hourai.bot import GuildSpecificCog


class GuildSpecific_TheGap(GuildSpecificCog):
    """ Guild specific code for The Gap, a server list server for Touhou related
    communities.
    """

    BIG_SERVER_SIZE = 250

    @commands.Cog.listener()
    async def on_message(self, msg):
        # Deletes any message that doesn't contain a server link.
        category = msg.channel.category
        if category is None or 'server' not in category.name.lower():
            return

        def on_error(e, t, tb):
            return logging.exception('Failed to get invite:')
        invites = await invite.get_all_discord_invites(
            self.bot, msg.content, on_error=on_error)
        invites = [inv for inv in invites if inv is not None]
        delete = len(invites) <= 0
        # If posted in #big-servers make sure it
        if 'big' in msg.channel.name:
            delete = delete or any(
                inv.approximate_member_count >= self.BIG_SERVER_SIZE
                for inv in invites)
        if delete:
            await msg.delete()


__GUILD_COGS = {
    GuildSpecific_TheGap: {355145270029451264},
}


def setup(bot):
    for cls, guilds in __GUILD_COGS.items():
        bot.add_cog(cls(bot, guilds=set(guilds)))
