import re
import inspect
from models import Session, Replacement

REPLACEMENT_REGEX = re.compile('{{(.*)}}')

def replace_values(session, input_str:str, context=None):
    current_str = input_str
    success = True
    while success:
        success, current_str = _replace_iteration(session, current_str)
    return current_str

def _replace_iteration(session, input_str:str, context=None):
    last_index = 0
    components = []
    for match in REPLACEMENT_REGEX.finditer(input_str):
        match_start, match_end = match.span(0)
        components.append(input_str[last_index:match_start])
        replacement = lookup_and_replace(session, match.group(1))
        components.append(match.group(0) if replacement is None else replacement)
        last_index = match_end + 1

    if last_index < len(input_str):
        components.append(input_str[last_index:])

    current_str = ''.join(components)

    return (current_str != input_str), current_str

def lookup_and_replace(session, input_str:str, context=None):
    key, end = tuple(input_str.split(' ', 1))
    replacement = session.query(Replacement).get(key)
    if replacement is None:
        return None
    context = dict(context or {}) 
    context['$input'] = end
    return substitute(replacement.response, context)

def substitute(val:str, replacements):
    """
    Replace values in a string based on a dict of values.
    If the values are executable, they will be called to generate
    a replacement if and only if the original is in the value.
    """
    for original, replacement in replacements.items():
        if original not in val:
            continue
        if callable(replacement):
            val = val.replace(original, replacement())
        else:
            val = val.replace(original, replacement)
    return val
