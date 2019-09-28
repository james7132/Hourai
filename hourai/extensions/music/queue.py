import asyncio
import collections
import random


class MusicQueue(asyncio.Queue):

    def __init__(self):
        self._queue = collections.OrderedDict()

    def _put(self, item):
        self._queue.setdefault(item[0], []).append(item[1])

    def _get(self):
        found = False
        while not found and len(self._queue) > 0:
            key, latest_queue = self._queue.popitem(last=False)
            found = len(latest_queue) > 0
        if not found:
            return None
        item = latest_queue.pop(0)
        if len(latest_queue) > 0:
            self._queue[key] = latest_queue
        return item

    def shuffle(self, key):
        """ Shuffles the items for a given key. """
        queue = self._queue.get(key)
        if queue is not None:
            random.shuffle(queue)

    def clear(self):
        """ Clears all elements in the queue. """
        self._queue.clear()
        self._wakeup_next(self._putters)

    def remove_all(self, key):
        """ Clears all elements in the queue with a given key. """
        if key in self._queue:
            count = len(self._queue[key])
            del self._queue[key]
            self._wakeup_next(self._putters)
            return count
        return 0

    def __iter__(self):
        iters = [iter(value) for value in self._queue.values()]
        progressed = True
        while progressed:
            progressed = False
            for iterator in iters:
                try:
                    yield next(iterator)
                    progressed = True
                except StopIteration:
                    pass

    def __len__(self):
        if len(self._queue) <= 0:
            return 0
        return sum(len(queue) for queue in self._queue.values())
