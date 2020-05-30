import asyncio
import collections
import random
from hourai.utils.iterable import first


class MusicQueue(asyncio.Queue):
    """An asyncio.Queue implementation that forms a FIFO, round-robin queue
    based on the provided key. The input is expected to be a tuple of
    (key, value). Backed by a collections.OrderedDict of lists, batch
    operations over individual queues for each key are either O(1) or O(k)
    operations, where k is the number of entries under that key.

    Example:
      Input: (a, 1), (a, 2), (b, 1), (c, 1), (a, 3), (b, 2)
      Output: (a, 1), (b, 1), (c, 1), (a, 2), (b, 2), (a, 3)
    """

    def _init(self, maxsize):
        self._queue = collections.OrderedDict()

    def _put(self, item):
        self._queue.setdefault(item[0], []).append(item[1])

    def _get(self):
        found = False
        while not found and len(self._queue) > 0:
            key, latest_queue = first(self._queue.items())
            if len(latest_queue) <= 1:
                # Any queue empty at the end of this pop needs to be removed
                self._queue.popitem(last=False)
            else:
                self._queue.move_to_end(key)
            found = len(latest_queue) > 0
        if not found:
            return None
        item = latest_queue.pop(0)
        return (key, item)

    def shuffle(self, key):
        """Shuffles the items for a given key. This is an O(k) operation, where
        k is the number of elements queued for the given key.
        """
        queue = self._queue.get(key)
        if queue is not None:
            random.shuffle(queue)
        return len(queue) if queue is not None else 0

    def clear(self):
        """Clears all elements in the queue. This is an O(1) operation."""
        self._queue.clear()
        self._wakeup_next(self._putters)

    def remove(self, idx):
        """Removes an item by it's index in the queue. This is a O(n) operation
        as it requires iteration through the queue.
        """
        if idx < 0 or idx >= len(self):
            raise IndexError
        for key, queue_idx, queue in self.__iter_self():
            if idx == 0:
                return (key, queue.pop(queue_idx))
            idx -= 1
        raise IndexError

    def remove_all(self, key):
        """Clears all elements in the queue with a given key. This is a O(1)
        operation.
        """
        if key in self._queue:
            count = len(self._queue[key])
            del self._queue[key]
            self._wakeup_next(self._putters)
            return count
        return 0

    def __getitem__(self, idx):
        """Gets an item by it's index in the queue. This is a O(n) operation as
        it requires iteration through the queue.
        """
        if idx < 0 or idx >= len(self):
            raise IndexError
        for key, queue_idx, queue in self.__iter_self():
            if idx == 0:
                return (key, queue[queue_idx])
            idx -= 1
        # This shouldn't happen with the initial check
        raise IndexError

    def __iter__(self):
        for key, queue_idx, queue in self.__iter_self():
            yield (key, queue[queue_idx])

    def __iter_self(self):
        indexes = collections.deque((key, 0) for key in self._queue.keys())
        while len(indexes) > 0:
            key, idx = indexes.popleft()
            queue = self._queue[key]
            if idx >= len(queue):
                continue
            yield (key, idx, queue)
            indexes.append((key, idx + 1))

    def __len__(self):
        if len(self._queue) <= 0:
            return 0
        return sum(len(queue) for queue in self._queue.values())
