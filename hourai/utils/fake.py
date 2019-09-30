import collections

_FAKE_MESSAGE_ATTRS = (
    'content', 'channel', 'guild', 'author', '_state'
)


FakeSnowflake = collections.namedtuple('FakeSnowflake', ('id',))


class FakeMessage:

    def __init__(self, **kwargs):
        msg = kwargs.pop('message', None)
        for attr in _FAKE_MESSAGE_ATTRS:
            if msg is not None and attr not in kwargs:
                setattr(self, attr, getattr(msg, attr, None))
            else:
                setattr(self, attr, kwargs.pop(attr, None))
