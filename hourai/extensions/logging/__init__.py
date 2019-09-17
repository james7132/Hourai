from .mod_logging import ModLogging

def setup(bot):
    bot.add_cog(ModLogging(bot))
