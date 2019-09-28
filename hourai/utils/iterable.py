from collections.abc import Iterable


def flatten(iterable):
    for val in iterable:
        if isinstance(val, Iterable):
            yield from flatten(val)
        else:
            yield val


def distinct(iterable):
    seen = set()
    for val in iterable:
        if val not in seen:
            yield val
            seen.add(val)
