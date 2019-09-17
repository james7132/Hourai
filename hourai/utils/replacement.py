import re
import inspect

class StringReplacer:

    def __init__(self, substitutions):
        substrings = sorted(substitutions, key=len, reverse=True)
        self.regex = re.compile('|'.join(map(re.escape, substrings)))
        self.substitutions = substitutions

    def subsittute(original, context=None, repeats=0):
        """
        A version of str.replace that supports providing multiple string matches.

            original[str]- the string to search substitutes in.
            substitutions[dict] -
                a mapping between substrings to search for and their replacements.
                If context is None, this is a string-string mapping. otherwise,
                it's a string-function mapping, where the functions are single
                argument that are passed the context object.
            context[any] - a parameterization for the subsittutions.
            repeats[int] -
                the number of times to repeat the substitution. The repeat will
                short circuit as soon as there is nothing to replace, thus it is
                safe to set this to arbitrarily large values.
        """
        if context is None:
            sub_func = lambda m: self.substitutions[m.group(0)]
        else:
            sub_func = lambda m: self.substitutions[m.group(0)](context)
        last = original
        while True:
            current = self.regex.sub(sub_func, last)
            if repeats > 0 or current == last:
                break
        return current
