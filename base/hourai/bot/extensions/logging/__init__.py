from .mod_logging import ModLogging
from .username_logging import UsernameLogging
from .owner_logging import OwnerLogging
from .role_logging import RoleLogging
from .ban_logging import BanLogging
from .counters import Counters


def setup(bot):
    cogs = (ModLogging(bot),
            UsernameLogging(bot),
            OwnerLogging(bot),
            RoleLogging(bot),
            BanLogging(bot),
            Counters(bot))
    for cog in cogs:
        bot.add_cog(cog)
