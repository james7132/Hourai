import asyncio
import functools
from abc import abstractmethod, ABC
from hourai import utils
from coders import IdentityCoder


class BackingStore(ABC):

    @abstractmethod
    async def get(self, key):
        raise NotImplementedError

    @abstractmethod
    async def set(self, key, value):
        raise NotImplementedError

    @abstractmethod
    async def clear(self, key):
        raise NotImplementedError

    async def get_all(self, keys):
        tasks = [(key, self.get(key)) for key in keys]
        await asyncio.gather(*[task for key, task in tasks])
        return {key: task.result() for key, task in tasks}

    async def set_all(self, mapping):
        await asyncio.gather(*[
            self.set(key, value) for key, value in mapping.items()
        ])

    async def clear_all(self, keys):
        await asyncio.gather(*[self.clear(key) for key in keys])


class RedisStore(BackingStore):

    def __init__(self, redis, timeout=0):
        self.redis = redis
        self.timeout = timeout

    async def get(self, key):
        return await self.redis.get(key)

    async def set(self, key, value):
        await self.redis.set(key, value, expire=self.timeout)

    async def clear(self, key):
        await self.redis.delete(key)

    async def clear_all(self, keys):
        await self._batch_do(keys, lambda tr, key: tr.delete(key))

    async def _batch_do(self, iterable, func):
        tr = self.redis.multi_exec()
        tasks = [func(tr, value) for value in iterable]
        results = await tr.execute()
        results_check = await asyncio.gather(*tasks)
        assert results == results_check
        return results


class RedisHashStore(RedisStore):
    """ Stores values in Redis hash fields. Key value is a 2-Tuple of
    (key, field)
    """

    async def get(self, key):
        return await self.redis.hget(*key)

    async def set(self, key, value):
        if self.timeout == 0:
            await self.redis.hset(key[0], key[1], value)
            return
        tr = self.redis.multi_exec()
        tr.hset(key[0], key[1], value)
        tr.expire(key[0], self.timeout)
        await tr.execute()

    async def clear(self, key):
        await self.redis.hdel(key[0], key[1])

    async def get_all(self, keys):
        results = await self._batch_do(keys, lambda tr, key: tr.hget(*key))
        return dict(zip(keys, results))

    async def set_all(self, mapping):
        tasks = []
        tr = self.redis.multi_exec()
        for key, group in self._group_by_key(mapping):
            tasks.append(tr.hmset_dict(key, group))
            if self.timeout != 0:
                tasks.append(tr.expire(key, self.timeout))
        results = await tr.execute()
        results_check = await asyncio.gather(*tasks)
        assert results == results_check

    async def clear_all(self, keys):
        await self._batch_do(keys, lambda tr, key: tr.hdel(*key))

    def _group_by_key(self, mapping):
        groups = {}
        for key, value in mapping.items():
            key, field = key
            if key not in groups:
                groups[key] = []
            groups[key].append((field, value))
        return ((key, dict(group)) for key, group in groups.items())

class Cache(BackingStore):
    """A wrapper around a backing store for caching data."""

    def __init__(self, store, *,
                 key_coder=IdentityCoder,
                 value_coder=IdentityCoder,
                 timeout=0):
        self.store = store
        self.key_coder = key_coder
        self.value_coder = value_coder

    async def set(self, key, message):
        await self.store.set(self.key_coder.encode(key),
                             self.value_coder.encode(message))

    async def get(self, key):
        value = await self.store.get(self.key_coder.encode(key))
        if value is None:
            return None
        return self.value_coder.decode(value)

    async def clear(self, key):
        await self.store.clear(self.key_coder.encode(key))

    async def get_all(self, keys):
        encoded_keys = [self.key_coder.encode(key) for key in keys]
        results = await self.store.get_all(encoded_keys)
        return {self.key_coder.decode(key):
                self.value_coder.decode(value) if value is not None else None
                for key, value in results.items()}

    async def set_all(self, mapping):
        mapping = {self.key_coder.encode(key): self.value_coder.encode(value)
                   for key, value in mapping.items()}
        await self.store.set_all(mapping)

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
