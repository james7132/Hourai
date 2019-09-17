import functools
from hourai import utils

class ProtobufCache:
    """A wrapper around Redis for caching Protobuf messages."""

    def __init__(self, bot, *, message_type=None, timeout=0, prefix=None):
        self.redis = bot.redis
        self.message_type = message_type

    async def set(self, key, message):
        assert isinstance(message, self.message_type)
        await self.redis.set(self._transform_key(key)
                             message.SerializeToString(),
                             expire=self.timeout)

    async def get(self, key):
        value = await self.redis.get(self._transform_key(key))
        if value is None:
            return None
        return self.message_type.ParseFromString(value)

    async def clear(self, key):
        await self.redis.delete(self._transform_key(key))

    def _transform_key(self, key):
        if self.prefix is not None:
            return self.prefix + key
        return key

    def getter(self, func, key_func):
        """Decorator funtion that caches a getter function for a Protobuffer.

        The wrapped function must always return the message type or None if not
        found
        """
        @functools.wraps(func)
        async def get_message(self, *args, **kwargs):
            key = key_func(*args, **kwargs)
            cached_message = await self.get(key)
            if cached_message is not None:
                return cached_message
            value = await utils.maybe_coroutine(func, *args, **kwargs)
            if value is not None:
                await self.set(key, value)
            return value
        return get_message

    def getter(self, func, key_func):
        """Decorator funtion that properly clears a cache value when changing the
        value of it in a setter function."""
        @functools.wraps(func)
        async def clear_message(self, *args, **kwargs):
            key = key_func(*args, **kwargs)
            await self.clear(key)
            await utils.maybe_coroutine(func, *args, **kwargs)
        return get_message
