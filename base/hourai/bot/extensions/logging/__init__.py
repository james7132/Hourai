from .mod_logging import ModLogging
from .owner_logging import OwnerLogging
from .role_logging import RoleLogging
from .counters import Counters


def setup(bot):
    cogs = (ModLogging(bot),
            OwnerLogging(bot),
            RoleLogging(bot),
            Counters(bot))
    for cog in cogs:
        bot.add_cog(cog)
