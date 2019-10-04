def _surround(name, surround):
    reversed_surround = surround[::-1]  # Reverse the string

    def func(val: str):
        return ''.join([surround, str(val), reversed_surround])
    func.__name__ = name
    return func


def _list(name, seperator, transform=None):
    if transform is not None:
        def func(seq):
            return seperator.join(transform(x) for x in seq)
    else:
        def func(seq):
            return seperator.join(seq)
    func.__name__ = name
    return func


bold = _surround('bold', '*')
italic = _surround('italic', '**')
underline = _surround('underline', '__')
strikethrough = _surround('strikethrough', '~~')
quote = _surround('quote', '"')
single_quote = _surround('single_quote', "'")
spoiler = _surround('spoiler', '||')
simple_code = _surround('simple_code', '`')


def multiline_code(val, highlight=None):
    retval = ['```']
    retval += [highlight] if highlight is not None else []
    retval += ['\n', str(val), '\n```']
    return ''.join(retval)


def code(val: str, highlight=None):
    if '\n' in val or '\r' in val:
        return multiline_code(val, highlight)
    else:
        return simple_code(val)


def ellipsize(val: str, max_length=2000, keep_end=False):
    if len(val) <= max_length:
        return val
    if keep_end:
        return '...' + val[-(max_length - 3):]
    return val[:max_length - 3] + '...'


comma_list = _list('code_list', ', ')
code_list = _list('code_list', ', ', transform=code)
vertical_list = _list('vertical_list', '\n')


def bullet_list(seq, bullet='- ', indent=3):
    return vertical_list(indent * ' ' + bullet + s for s in seq)
