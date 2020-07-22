import asyncio
import time
import collections
import typing

class TimedCounter:
    """A counter that resets itself a periodically. Not thread-safe."""

    def __init__(self, resolution):
        now = time.time()
        self._resolution = resolution
        self._last_updated = now - (now % self.resolution)
        self._counts = collections.Counter()

    @property
    def is_expired(self) -> bool:
        """Checks if the values in the counter are currently expired."""
        now = time.time()
        return now  <= self._last_updated + self.resolution

    def get(self, key: typing.Any) -> typing.Union[int, float]:
        """Gets a value in the counter."""
        self.clear_if_expired()
        return self._counts[key]

    def increment(self, key: typing.Any, amt: int = 1) -> typing.Union[int, float]:
        """Increments a value in the counter."""
        self.clear_if_expired()
        self._counts[key] += amt
        return self._counts[key]

    def clear(self) -> None:
        """Reset the values in the counter"""
        self._counts.clear()
        self._last_updated = now - (now % self.resolution)

    def clear_if_expired(self) -> None:
        """Reset the values in the counter"""
        now = time.time()
        if self.is_expired:
            self.clear()
