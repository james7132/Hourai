from .discord_bot_list import TopGG

def setup(bot):
    bot.add_cog(TopGG(bot))
