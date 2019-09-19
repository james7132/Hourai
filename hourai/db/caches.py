import functools
import struct
from abc import abstractmethod
from hourai import utils

class Coder:

    @abstractmethod
    def serialize(self, msg):
        pass

    @abstractmethod
    def deserialize(self, buf):
        pass

class BackingStore:

    @abstractmethod
    async def get(self, key):
        pass

    @abstractmethod
    async def set(self, key, value):
        pass

    @abstractmethod
    async def clear(self, key):
        pass

class IdentityCoder(Coder):

    def serialize(self, msg):
        return msg

    def deserialize(self, buf):
        return buf

class ChainCoder(Coder):

    def __init__(self, sub_coders):
        self.serialize_coders = tuple(sub_coders)
        self.deserialize_coders = tuple(reversed(sub_coders))

    def serialize(self, msg):
        return reduce(lambda c, m: c.serialize(m),
                      self.serialize_coders, msg)

    def deserialize(self, buf):
        return reduce(lambda c, m: c.deserialize(m),
                      self.deserialize_coders, buf)

class IntCoder(Coder):

    def __init__(self, format=">Q"):
        self.format = format

    def serialize(self, msg):
        return struct.pack(self.format, msg)

    def deserialize(self, buf):
        return struct.unpack(self.format, buf)

class PrefixCoder(Coder):

    def __init__(self, prefix):
        self.prefix = prefix

    def serialize(self, msg):
        return prefix + msg

    def deserialize(self, buf):
        return buf.replace(self.prefix, '')

class ProtobufCoder(Coder):

    def __init__(self, message_type):
        self.message_type = message_type

    def serialize(self, msg):
        return msg.MessageToString()

    def deserialize(self, buf):
        return self.message_type.ParseFromString(buf)

class RedisStore(BackingStore):

    def __init__(self, redis):
        self.get = redis.get
        self.set = redis.set
        self.clear = redis.delete

class Cache(BackingStore):
    """A wrapper around a backing store for caching data."""

    def __init__(self, store, *,
                 key_coder=IdentityCoder,
                 value_coder=IdentityCoder,
                 timeout=0):
        self.store = store
        self.key_coder = key_coder
        self.value_coder = value_coder
        self.timeout = timeout

    async def set(self, key, message):
        assert isinstance(message, self.message_type)
        await self.store.set(self.key_coder.serialize(key),
                             self.value_coder.serialize(message),
                             expire=self.timeout)

    async def get(self, key):
        value = await self.store.get(self.key_coder.serialize(key))
        if value is None:
            return None
        return self.value_coder.deserialize(value)

    async def clear(self, key):
        await self.store.clear(self.key_coder.serialize(key))

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
