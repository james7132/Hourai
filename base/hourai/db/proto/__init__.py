from .action_pb2 import *  # noqa
from .auto_config_pb2 import *  # noqa
from .ban_pb2 import *  # noqa
from .escalation_pb2 import *  # noqa
from .event_pb2 import *  # noqa
from .guild_configs_pb2 import *  # noqa


def get_field(msg, field):
    return getattr(msg, field) if msg.HasField(field) else None
