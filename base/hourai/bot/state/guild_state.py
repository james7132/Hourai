from datetime import datetime


class GuildState:
    """Ephemeral state associated with a Discord guild. Lost on bot restart.
    """

    __slots__ = ['_lockdown_expiration']

    def __init__(self):
        self._lockdown_expiration = None

    @property
    def is_locked_down(self):
        return self._lockdown_expiration is not None and \
               datetime.utcnow() < self._lockdown_expiration

    def set_lockdown(self, state, expiration=datetime.max):
        self._lockdown_expiration = None if not state else expiration
