import asyncio
import collections
import functools
from abc import abstractmethod, ABC
from hourai import utils
from coders import IdentityCoder
from .redis_utils import redis_transaction


class KeyValueStore(ABC):

    @abstractmethod
    async def get(self, key):
        """|coro| Gets the value for a key. Generally should be an atomic
        operation, but may not be depending on implementation.
        """
        raise NotImplementedError

    @abstractmethod
    async def set(self, key, value):
        """|coro| Sets the value for a key. If it doesn't already exist, it
        should be created. Generally should be an atomic operation, but may not
        be depending on implementation.
        """
        raise NotImplementedError

    @abstractmethod
    async def clear(self, key):
        """|coro| Deletes the value for a key. Generally should be an atomic
        operation, but may not be depending on implementation.
        """
        raise NotImplementedError

    async def get_all(self, keys):
        """|coro| Gets the values for multiple keys. Generally should be an
        atomic operation, but may not be depending on implementation.
        """
        tasks = [(key, self.get(key)) for key in keys]
        await asyncio.gather(*[task for key, task in tasks])
        return {key: task.result() for key, task in tasks}

    async def set_all(self, mapping):
        """|coro| Sets the values for multiple keys. Generally should be an
        atomic operation, but may not be depending on implementation.
        """
        await asyncio.gather(*[
            self.set(key, value) for key, value in mapping.items()
        ])

    async def clear_all(self, keys):
        """|coro| Deletes the values for multiple keys. Generally should be an
        atomic operation, but may not be depending on implementation.
        """
        await asyncio.gather(*[self.clear(key) for key in keys])


class RedisStore(KeyValueStore):

    def __init__(self, redis, timeout=0):
        self.redis = redis
        self.timeout = timeout

    async def get(self, key):
        """|coro| Gets the value for a key. Is an atomic operation."""
        return await self.redis.get(key)

    async def set(self, key, value):
        """|coro| Sets the value for a key. Is an atomic operation."""
        await self.redis.set(key, value, expire=self.timeout)

    async def clear(self, key):
        """|coro| Deletes the value for a key. Is an atomic operation."""
        await self.redis.delete(key)

    async def get_all(self, keys):
        """|coro| Gets the values for multiple keys. Is an atomic operation."""
        return await self._batch_do(keys, lambda tr, key: tr.get(key))

    async def set_all(self, mapping):
        """|coro| Sets the values for multiple keys. Is an atomic operation."""
        await self._batch_do(mapping.items(),
                lambda tr, entry: tr.set(etnry[0], entry[1]))

    async def clear_all(self, keys):
        """|coro| Deletes the value for a key. Is an atomic operation."""
        await self._batch_do(keys, lambda tr, key: tr.delete(key))

    async def _batch_do(self, iterable, func):
        """|coro| Runs identical operations over every item in an iterable as an
        atomic transaction. func is a function that takes (transaction, value)
        as an input."""
        await self._transaction(
                lambda tr: (func(tr, value) for value in iterable))

    async def _transaction(self, txn_fn):
        """|coro| Runs arbitrary operations as an atomic Redis transaction.
        txn_fn is a finite generator function that takes a transaction as an
        argument and yields awaitable transaction operations."""
        return await redis_transaction(self.redis, txn_fn)


class RedisHashStore(RedisStore):
    """ Stores values in Redis hash fields. Key value is a 2-Tuple of
    (key, field).
    """

    async def get(self, key):
        """|coro| Gets the value for a key and field. Is an atomic operation."""
        return await self.redis.hget(*key)

    async def set(self, key, value):
        """|coro| Sets the value for a key and field. Is an atomic operation."""
        if self.timeout <= 0:
            await self.redis.hset(key[0], key[1], value)
            return

        def txn_fn(tr):
            yield tr.hset(key[0], key[1], value)
            yield tr.expire(key[0], self.timeout)
        await self._transaction(txn_fn)

    async def clear(self, key):
        """|coro| Deletes the value for a key and field. Is an atomic operation.
        """
        await self.redis.hdel(key[0], key[1])

    async def get_all(self, keys):
        """|coro| Get the values for a set of keys and fields. Is an atomic
        operation.
        """
        results = await self._batch_do(keys, lambda tr, key: tr.hget(*key))
        return dict(zip(keys, results))

    async def set_all(self, mapping):
        """|coro| Set the values for a set of keys and fields. Is an atomic
        operation.
        """
        def txn_fn(tr):
            for key, group in self._group_by_key(mapping):
                yield tr.hmset_dict(key, group)
                if self.timeout > 0:
                    yield tr.expire(key, self.timeout)
        await self._transaction(txn_fn)

    async def clear_all(self, keys):
        """|coro| Delete the values for set of keys and fields. Is an atomic
        operation.
        """
        await self._batch_do(keys, lambda tr, key: tr.hdel(*key))

    def _group_by_key(self, mapping):
        groups = {}
        for key, value in mapping.items():
            key, field = key
            if key not in groups:
                groups[key] = []
            groups[key].append((field, value))
        return ((key, dict(group)) for key, group in groups.items())


class Cache(KeyValueStore):
    """A wrapper around a backing store for caching data."""

    def __init__(self, store, *,
                 key_coder=IdentityCoder(),
                 value_coder=IdentityCoder(),
                 timeout=0):
        self.store = store
        self.key_coder = key_coder
        self.value_coder = value_coder

    async def get(self, key):
        """|coro| Get the value for a key and field. Atomicity depends on
        underlying store.
        """
        value = await self.store.get(self.key_coder.encode(key))
        if value is None:
            return None
        return await self.value_coder.decode(value)

    async def set(self, key, message):
        """|coro| Sets the value for a key and field. Atomicity depends on
        underlying store.
        """
        await self.store.set(self.key_coder.encode(key),
                             self.value_coder.encode(message))

    async def clear(self, key):
        """|coro| Deletes the value for a key and field. Atomicity depends on
        underlying store.
        """
        await self.store.clear(self.key_coder.encode(key))

    async def get_all(self, keys):
        """|coro| Deletes the value for a key and field. Atomicity depends on
        underlying store.
        """
        encoded_keys = [self.key_coder.encode(key) for key in keys]
        results = await self.store.get_all(encoded_keys)
        return {self.key_coder.decode(key):
                self.value_coder.decode(value) if value is not None else None
                for key, value in results.items()}

    async def set_all(self, mapping):
        """|coro| Deletes the value for a key and field. Atomicity depends on
        underlying store.
        """
        mapping = {self.key_coder.encode(key): self.value_coder.encode(value)
                   for key, value in mapping.items()}
        await self.store.set_all(mapping)

    def getter(self, func, key_func):
        """Decorator function that caches a getter function for a Protobuffer.

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

    def setter(self, func, key_func):
        """Decorator funtion that properly clears a cache value when changing
        the value of it in a setter function.
        """
        @functools.wraps(func)
        async def clear_message(self, *args, **kwargs):
            key = key_func(*args, **kwargs)
            await self.clear(key)
            await utils.maybe_coroutine(func, *args, **kwargs)
        return clear_message


class AggregateProtoHashCache(RedisStore):
    """A KeyValueStore that stores protocol buffers in Redis hashes."""

    class Entry(collections.namedtuple('_Entry',
                                       'field field_name value_coder')):
        pass

    def __init__(self, redis, msg_type, *, value_coders,
                 timeout=0, key_coder=IdentityCoder()):
        super().__init__(redis, timeout=timeout)
        self.msg_type = msg_type
        self.key_coder = key_coder
        self.value_coders = value_coders
        self._fields = [entry.field for entry in value_coders]
        self._validate_config()

    def _validate_config(self):
        proto_val = self.msg_type
        for entry in self.value_coders:
            assert hasattr(proto_val, entry.field_name), \
                f"{self.msg_type} does not have attribute {entry.field_name}"

    async def get(self, key):
        """|coro| Gets the value for a key. Is atomic."""
        key_enc = self.key_coder.encode(key)
        proto_val = self.msg_type()

        results = await self.redis.hmget(key_enc, *self._fields)
        for entry, result_enc in zip(self.value_coders, results):
            if result_enc is None:
                continue
            result = entry.value_coder.decode(result_enc)
            getattr(proto_val, entry.field_name).CopyFrom(result)

        return proto_val

    async def set(self, key, value):
        """|coro| Sets the value for a key and field. Is atomic."""
        assert isinstance(value, self.msg_type)

        key_enc = self.key_coder.encode(key)
        fields = {}
        missing_fields = []
        for entry in self.value_coders:
            if value.HasField(entry.field_name):
                fields[entry.field] = entry.value_coder.encode(
                        getattr(value, entry.field_name))
            else:
                missing_fields.append(entry.field)

        def txn_fn(tr):
            if len(fields) > 0:
                yield tr.hmset_dict(key_enc, fields)
            if len(missing_fields) > 0:
                yield tr.hdel(key_enc, *missing_fields)
            if self.timeout > 0:
                yield tr.expire(key_enc, self.timeout)
        await self._transaction(txn_fn)

    async def clear(self, key):
        """|coro| Deletes the value for a key and field. Is atomic."""
        await self.redis.delete(self.key_coder.encode(key))


class AggregateProtoCache:

    def __init__(self, msg_type, mappings):
        self.msg_type = msg_type
        self._mappings = mappings
        self._validate_config()

    def _validate_config(self):
        proto_val = self.msg_type
        for attr in self._mappings.keys():
            assert hasattr(proto_val, attr), \
                f"{self.msg_type} does not have attribute {attr}"

    async def get(self, key):
        proto_val = self.msg_type()

        async def _get_field(attr, cache):
            result = await cache.get(key)
            if result is not None:
                getattr(proto_val, attr).CopyFrom(result)
        await asyncio.gather(*[_get_field(a, c) for a, c in self._mappings])
        return proto_val

    async def set(self, key, value):
        assert isinstance(value, self.msg_type)
        tasks = []
        for attr, cache in self._mappings:
            if value.HasField(attr):
                tasks.append(cache.set(key, getattr(value, attr)))
        await asyncio.gather(*tasks)

    async def clear(self, key):
        await asyncio.gather(*[cache.clear(key)
                               for _, cache in self._mappings])
