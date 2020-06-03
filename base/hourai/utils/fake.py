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


class FakeContextManager:
    """A context manager that does nothing. Used as a non-None value where a
    context manager is needed but no action is required.

    Works as both a regular context manager and an async context manager, and
    thus works with both `with` and `async with`.

    Any exceptions encountered will be reraised.
    """

    def __enter__(self):
        return self

    def __exit__(self, type, exc, tb):
        raise exc

    async def __aenter__(self):
        return self

    async def __aexit__(self, type, exc, tb):
        return self.__exit__(type, exc, tb)
