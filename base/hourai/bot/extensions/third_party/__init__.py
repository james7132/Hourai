from .top_gg import TopGG
from .discord_bots import DiscordBots

def setup(bot):
    bot.add_cog(DiscordBots(bot))
    bot.add_cog(TopGG(bot))
