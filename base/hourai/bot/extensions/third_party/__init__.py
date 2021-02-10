from .discord_bots import DiscordBots
from .discord_boats import DiscordBoats
from .top_gg import TopGG


def setup(bot):
    bot.add_cog(DiscordBoats(bot))
    bot.add_cog(DiscordBots(bot))
    bot.add_cog(TopGG(bot))
