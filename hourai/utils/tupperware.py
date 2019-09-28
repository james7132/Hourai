# A convert of JSON configs into more readily usable immutable Python objects
# Mainly used for construction of config objects.
#
# Shamelessly copied and modified from
# https://gist.github.com/sherzberg/7661076

import collections
from collections.abc import Mapping, MutableMapping, Sequence


def tupperware(mapping):
    """ Convert mappings to 'tupperwares' recursively.
    Lets you use dicts like they're JavaScript Object Literals (~=JSON)...
    It recursively turns mappings (dictionaries) into namedtuples.
    Thus, you can cheaply create an object whose attributes are accessible
    by dotted notation (all the way down).
    Use cases:
        * Fake objects (useful for dependency injection when a Mock is actually
        more complex than your requirements call for)
        * Storing data (like fixtures) in a structured way, in Python code
        (data whose initial definition reads nicely like JSON). You could do
        this with dictionaries, but this solution is immutable, and its
        dotted notation is arguably clearer in many contexts.
    .. doctest::
        >>> t = tupperware({
        ...     'foo': 'bar',
        ...     'baz': {'qux': 'quux'},
        ...     'tito': {
        ...         'tata': 'tutu',
        ...         'totoro': 'tots',
        ...         'frobnicator': ['this', 'is', 'not', 'a', 'mapping']
        ...     },
        ...     'alist': [
        ...         {'one': '1', 'a': 'A'},
        ...         {'two': '2', 'b': 'B'},
        ...     ]
        ... })
        >>> t # doctest: +ELLIPSIS
        Tupperware(baz=Tupperware(qux='quux'), tito=Tupperware(...), foo='bar',
                   alist=[Tupperware(...), Tupperware(...)])
        >>> t.tito # doctest: +ELLIPSIS
        Tupperware(frobnicator=[...], tata='tutu', totoro='tots')
        >>> t.tito.tata
        'tutu'
        >>> t.tito.frobnicator
        ['this', 'is', 'not', 'a', 'mapping']
        >>> t.foo
        'bar'
        >>> t.baz.qux
        'quux'
        >>> t.alist[0].one
        '1'
        >>> t.alist[0].a
        'A'
        >>> t.alist[1].two
        '2'
        >>> t.alist[1].b
        'B'
    Args:
        mapping: An object that might be a mapping. If it's a mapping, convert
        it (and all of its contents that are mappings) to namedtuples
        (called 'Tupperwares').
    Returns:
        A tupperware (a namedtuple (of namedtuples (of namedtuples (...)))).
        If argument is not a mapping, it just returns it (this enables the
        recursion).
    """

    if isinstance(mapping, Mapping):
        for key, value in mapping.items():
            mapping[key] = tupperware(value)
        return __namedtuple_wrapper(**mapping)
    elif isinstance(mapping, Sequence) and not isinstance(mapping, str):
        return tuple(tupperware(item) for item in mapping)
    return mapping


def __namedtuple_wrapper(**kwargs):
    namedtuple = collections.namedtuple('Tupperware', kwargs)
    return namedtuple(**kwargs)


# Filler value to avoid equating to None in the case it appears
__END = collections.namedtuple('__END', ())


def conform(val, template, default=None):
    """Conforms a deserialized form to a provided template."""
    __conform(val, template, default, '')


def __conform(base, template, default, path):
    if isinstance(base, ProtectedDict):
        return
    elif isinstance(base, MutableMapping):
        assert isinstance(base, Mapping) and isinstance(template, Mapping)
        for key in base:
            if key not in template:
                del base[key]
            else:
                __conform(base[key], template[key], default,
                          f'{path}.{str(key)}')
        for key in template:
            if key not in base:
                base[key] = default
        return
    elif isinstance(base, Sequence):
        # Length does not need to match, but at least one
        assert isinstance(base, Sequence) and isinstance(template, Sequence)
        target_template = next(iter(template), __END)
        if target_template is __END:
            raise ValueError(f"No template for values in '{path}'.")
        idx = 0
        for val in base:
            __conform(val, target_template, default, f'{path}.{str(idx)}')
            idx = idx + 1
        return
    if type(base) != type(template) and base is not None:
        raise ValueError(
            f"Types for path '{path}' don't match {type(base)} vs " +
            f"{type(template)}.")


class ProtectedDict(collections.UserDict):
    """ A class that exists just to tell `tupperware` not to eat it.
    `tupperware` eats all dicts you give it, recursively; but what if you
    actually want a dictionary in there? This will stop it. Just do
    ProtectedDict({...}) or ProtectedDict(kwarg=foo).
    """
