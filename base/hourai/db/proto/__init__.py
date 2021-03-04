from discord.flags import BaseFlags, flag_value, fill_with_flags
from .action_pb2 import *  # noqa
from .auto_config_pb2 import *  # noqa
from .cache_pb2 import *  # noqa
from .ban_pb2 import *  # noqa
from .escalation_pb2 import *  # noqa
from .event_pb2 import *  # noqa
from .guild_configs_pb2 import *  # noqa


def get_field(msg, field):
    return getattr(msg, field) if msg.HasField(field) else None


@fill_with_flags()
class RoleFlags(BaseFlags):
    __slots__ = ()

    def __init__(self, flags=0, **kwargs):
        if not isinstance(flags, int):
            raise TypeError(
                    'Expected int parameter, received %s instead.' %
                    flags.__class__.__name__)

        self.value = flags
        for key, value in kwargs.items():
            if key not in self.VALID_FLAGS:
                raise TypeError('%r is not a valid permission name.' % key)
            setattr(self, key, value)

    @flag_value
    def dj(self):
        return 1 << 0

    @flag_value
    def moderator(self):
        return 1 << 1

    @flag_value
    def restorable(self):
        return 1 << 2
