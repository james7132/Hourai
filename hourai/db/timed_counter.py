import asyncio
import coders
import collections
import enum
import time
from datetime import timedelta
from abc import ABC, abstractmethod


BYTE_MAX = 2 ** 8 - 1
TimedCounterConfig = collections.namedtuple(
    'TimedCounterConfig', 'prefix length')


class TimedCounterResolutions(enum.Enum):
    ONE_SECOND = TimedCounterConfig(prefix=enum.auto(),
                                    length=timedelta(seconds=1))
    TEN_SECONDS = TimedCounterConfig(prefix=enum.auto(),
                                     length=timedelta(seconds=10))
    ONE_MINUTE = TimedCounterConfig(prefix=enum.auto(),
                                    length=timedelta(minutes=1))
    TEN_MINUTES = TimedCounterConfig(prefix=enum.auto(),
                                     length=timedelta(minutes=10))
    ONE_HOUR = TimedCounterConfig(prefix=enum.auto(),
                                  length=timedelta(hours=1))


class TimedCounterStorageBase(ABC):

    def __init__(self, *, redis, prefix, resolution):
        self.redis = redis
        self.prefix = prefix
        self.resolution = resolution

        self.key_coder = self._build.key_coder()

    @abstractmethod
    async def get_count(self, resource, field, timestamp):
        raise NotImplementedError()

    def _build_key_coder(self):
        return coders.TupleCoder([
            coders.UInt64Coder(), coders.IdentityCoder()
        ]).prefixed(self.prefix + bytes([self.resolution.prefix]))

    def _get_key(self, resource, field, timestamp=None):
        """Gets the corresponding key for a resource and a timestamp."""
        return self.key_coder.encode((
            resource.id, self._get_time_suffix(timestamp)))

    def _get_time_suffix(self, timestamp):
        """Gets the time bucket ID for a resource and a timestamp."""
        return bytes([self.__get_bucket(timestamp) % BYTE_MAX])

    def _get_expiration(self, timestamp):
        """Gets the expiration timestamp a timestamp."""
        bucket = self.__get_bucket(timestamp)
        return (bucket + 1) * self.resolution.length.total_seconds()

    def __get_bucket(self, timestamp):
        return int(timestamp) // self.resolution.length.total_seconds()


class TimedCounterStorage:
    """Redis-based ephemeral counter storage.

    Root Keyspace Structure: "<prefix><resolution><resource id><timestamp>"
    Redis Data Type: Hash (field -> value)

    Minimum time resolution is 1 second.
    """

    def get_count(self, resource, field, timestamp):
        key = self._get_key(resource, field, timestamp)
        return self.redis.hget(key, field)

    def increment(self, resource, field, timestamp=None):
        """|coro|

        Increments the counter.

        Returns: the value of the counter, post-increment.
        """
        return self.increment_by(resource, field, count=1,
                                 timestamp=timestamp)

    def decrement(self, resource, field, timestamp=None):
        """|coro|

        Decrements the counter.

        Returns: the value of the counter, post-decrement.
        """
        return self.increment_by(resource, field, count=-1,
                                 timestamp=timestamp)

    async def increment_by(self, resource, field, count, timestamp=None):
        """|coro|

        Increments the counter by a specified value. Supports negative values
        for decrements.

        Returns: the value of the counter, post-increment.
        """
        if timestamp is None:
            timestamp = int(time.time())

        key = self._get_key(resource, field, timestamp)
        expire_timestamp = self._get_expiration(timestamp)

        tr = self.redis.multi_exec()
        fut_inc = tr.hincrby(key, field, increment=count)
        fut_exp = tr.expireat(key, expire_timestamp)
        res = await tr.execute()
        ret = await asyncio.gather(*[fut_inc, fut_exp])
        assert res == ret

        return await fut_inc


class TimedUniqueCounterStorage:
    """Redis-based ephemeral unique counter storage.

    Root Keyspace Structure:
        "<prefix><resolution><resource id><field><timestamp>"
    Redis Data Type: Set

    Minimum time resolution is 1 second.
    """

    def get_count(self, resource, field, timestamp):
        key = self._get_key(resource, field, timestamp)
        return self.redis.scard(key)

    def __init__(self, *, redis, prefix, resolution,
                 field_coder=coders.IdentityCoder(),
                 value_coder=coders.IdentityCoder()):
        super().__init__(redis=redis, prefix=prefix, resolution=resolution)
        assert value_coder is not None
        self.field_coder = field_coder
        self.value_coder = value_coder

    def _build_key_coder(self):
        return coders.TupleCoder([
            coders.UInt64Coder(),
            self.field_coder,
            coders.IdentityCoder()
        ]).prefixed(self.prefix + bytes([self.resolution.prefix]))

    def _get_key(self, resource, field, timestamp=None):
        """Gets the corresponding key for a resource and a timestamp."""
        return self.key_coder.encode((
            resource.id, field, self._get_time_suffix(timestamp)))

    def add(self, resource, field, value, timestamp=None):
        return self.add_all(resource, field, [value], timestamp=timestamp)

    async def add_all(self, resource, field, values, timestamp=None):
        """|coro|

        Returns: the value of the counter, post-decrement.
        """
        if timestamp is None:
            timestamp = int(time.time())

        key = self._get_key(resource, timestamp)
        expire_timestamp = self._get_expiration(timestamp)

        encoded_values = [self.value_coder.encode(val) for val in values]

        tr = self.redis.multi_exec()
        fut_add = tr.sadd(key, *encoded_values)
        fut_cnt = tr.scard(key)
        fut_exp = tr.expireat(key, expire_timestamp)
        res = await tr.execute()
        ret = await asyncio.gather(*[fut_add, fut_cnt, fut_exp])
        assert res == ret

        return await fut_cnt
