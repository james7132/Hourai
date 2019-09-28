from .mod_logging import ModLogging
from .username_logging import UsernameLogging


def setup(bot):
    cogs = (ModLogging(bot), UsernameLogging(bot))
    for cog in cogs:
        bot.add_cog(cog)
