"""
Regression: str method TypeError messages carry the CPython wording with
the argument ordinal and the offending type name, and the numeric
converter arguments (split/rsplit maxsplit, replace count, center/ljust/
rjust/zfill width, expandtabs tabsize) convert through __index__ — bool
is accepted, float/bytes/None raise "'X' object cannot be interpreted as
an integer", and out-of-range ints raise the C-level OverflowError.
index/rindex have their own message templates instead of reusing
find/rfind, find/index sub checks precede the start/end checks, split
validates sep before maxsplit, None maxsplit/count is rejected, and
fillchar rejects None ("default by omission only").

CPython 3.14 reference (Objects/unicodeobject.c do_find/do_count/
do_replace/do_split/do_xsplit/unicode_center_impl/unicode_expandtabs,
clinic Py_ssize_t converters).
"""


def msg(fn):
    try:
        fn()
        return "ok"
    except TypeError as e:
        return "TypeError: " + str(e)
    except Exception as e:
        return type(e).__name__ + ": " + str(e)


# --- sub argument of find/index/rfind/rindex/count ---
assert msg(lambda: 'abc'.find(b'a')) == "TypeError: find() argument 1 must be str, not bytes"
assert msg(lambda: 'abc'.index(b'b')) == "TypeError: index() argument 1 must be str, not bytes"
assert msg(lambda: 'abc'.rfind(b'a')) == "TypeError: rfind() argument 1 must be str, not bytes"
assert msg(lambda: 'abc'.rindex(b'b')) == "TypeError: rindex() argument 1 must be str, not bytes"
assert msg(lambda: 'abc'.count(b'a')) == "TypeError: count() argument 1 must be str, not bytes"
assert msg(lambda: 'abc'.find(['a'])) == "TypeError: find() argument 1 must be str, not list"
# the sub check precedes the start/end checks
assert msg(lambda: 'abc'.find(b'a', b'0')) == "TypeError: find() argument 1 must be str, not bytes"
# str subclasses are accepted like CPython's PyUnicode_Check
class S(str):
    pass

assert 'abc'.find(S('b')) == 1
# start/end still use the slice converter
assert msg(lambda: 'abc'.find('a', b'0')) == "TypeError: slice indices must be integers or None or have an __index__ method"
assert msg(lambda: 'abc'.index('b', b'0')) == "TypeError: slice indices must be integers or None or have an __index__ method"

# --- replace ---
assert msg(lambda: 'abc'.replace(b'a', 'x')) == "TypeError: replace() argument 1 must be str, not bytes"
assert msg(lambda: 'abc'.replace('a', 5)) == "TypeError: replace() argument 2 must be str, not int"
assert msg(lambda: 'abc'.replace(b'a', 'x', b'2')) == "TypeError: replace() argument 1 must be str, not bytes"
# count is a strict __index__ converter: None rejected, bool accepted
assert msg(lambda: 'abc'.replace('a', 'x', None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert 'abc'.replace('a', 'x', True) == 'xbc'
assert msg(lambda: 'abc'.replace('a', 'x', 2.5)) == "TypeError: 'float' object cannot be interpreted as an integer"
assert msg(lambda: 'abc'.replace('a', 'x', 10**100)) == "OverflowError: Python int too large to convert to C ssize_t"

# --- split/rsplit ---
assert msg(lambda: 'a,b'.split(b',')) == "TypeError: must be str or None, not bytes"
assert msg(lambda: 'a,b'.split(3)) == "TypeError: must be str or None, not int"
assert msg(lambda: 'a,b'.split(True)) == "TypeError: must be str or None, not bool"
assert msg(lambda: 'abc'.rsplit(b',')) == "TypeError: must be str or None, not bytes"
# sep is validated before maxsplit
assert msg(lambda: 'a,b'.split(b',', 1)) == "TypeError: must be str or None, not bytes"
# maxsplit is a strict __index__ converter
assert msg(lambda: 'a,b'.split(',', b'1')) == "TypeError: 'bytes' object cannot be interpreted as an integer"
assert msg(lambda: 'abc'.rsplit(None, b'1')) == "TypeError: 'bytes' object cannot be interpreted as an integer"
assert msg(lambda: 'a,b'.split(',', None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert msg(lambda: 'aXbXc'.split('X', 2.5)) == "TypeError: 'float' object cannot be interpreted as an integer"
assert msg(lambda: 'aXbXc'.split('X', 10**100)) == "OverflowError: Python int too large to convert to C ssize_t"
assert 'a,b,c'.rsplit(',', 1) == ['a,b', 'c']

# --- partition/rpartition: CPython's bare converter message ---
assert msg(lambda: 'abc'.partition(b'b')) == "TypeError: must be str, not bytes"
assert msg(lambda: 'abc'.partition(3)) == "TypeError: must be str, not int"
assert msg(lambda: 'abc'.rpartition(b'b')) == "TypeError: must be str, not bytes"

# --- fill character ---
assert msg(lambda: 'a'.center(5, b'*')) == "TypeError: The fill character must be a unicode character, not bytes"
assert msg(lambda: 'a'.ljust(5, 3)) == "TypeError: The fill character must be a unicode character, not int"
assert msg(lambda: 'a'.rjust(5, 3)) == "TypeError: The fill character must be a unicode character, not int"
assert msg(lambda: 'a'.center(5, None)) == "TypeError: The fill character must be a unicode character, not NoneType"
assert msg(lambda: 'a'.center(5, '**')) == "TypeError: The fill character must be exactly one character long"
assert msg(lambda: 'a'.ljust(5, '')) == "TypeError: The fill character must be exactly one character long"

# --- widths convert through __index__ ---
assert msg(lambda: 'a'.center(b'5')) == "TypeError: 'bytes' object cannot be interpreted as an integer"
assert msg(lambda: 'a'.center(None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert msg(lambda: 'a'.center(2.5)) == "TypeError: 'float' object cannot be interpreted as an integer"
assert msg(lambda: 'a'.center(10**100)) == "OverflowError: Python int too large to convert to C ssize_t"
assert 'a'.center(True) == 'a'
assert 'a'.center(-1) == 'a'
assert 'a'.ljust(True) == 'a'
assert msg(lambda: 'a'.zfill(b'0')) == "TypeError: 'bytes' object cannot be interpreted as an integer"
assert msg(lambda: 'a'.zfill(2.5)) == "TypeError: 'float' object cannot be interpreted as an integer"
assert msg(lambda: 'a'.zfill(10**100)) == "OverflowError: Python int too large to convert to C ssize_t"
assert 'a'.zfill(True) == 'a'
assert 'a'.zfill(-1) == 'a'
assert msg(lambda: 'abc'.expandtabs(b'4')) == "TypeError: 'bytes' object cannot be interpreted as an integer"
assert msg(lambda: 'abc'.expandtabs(None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert msg(lambda: 'abc'.expandtabs(10**100)) == "OverflowError: Python int too large to convert to C int"
assert 'abc'.expandtabs(True) == 'abc'
assert 'abc'.expandtabs(-1) == 'abc'

# --- removeprefix/removesuffix ---
assert msg(lambda: 'abc'.removeprefix(b'a')) == "TypeError: removeprefix() argument must be str, not bytes"
assert msg(lambda: 'abc'.removesuffix(b'c')) == "TypeError: removesuffix() argument must be str, not bytes"
assert msg(lambda: 'abc'.removeprefix(3)) == "TypeError: removeprefix() argument must be str, not int"

# --- controls: already-correct faces stay correct ---
assert msg(lambda: 'abc'.startswith(b'a')) == "TypeError: startswith first arg must be str or a tuple of str, not bytes"
assert msg(lambda: 'abc'.endswith(b'c')) == "TypeError: endswith first arg must be str or a tuple of str, not bytes"
assert msg(lambda: 'abc'.strip(3)) == "TypeError: strip arg must be None or str"
assert msg(lambda: ','.join([b'a'])) == "TypeError: sequence item 0: expected str instance, bytes found"

# functional behavior unchanged
assert 'a,b,c'.split(',', 1) == ['a', 'b,c']
assert 'a,b,c'.rsplit(',', 1) == ['a,b', 'c']
assert 'abcabc'.replace('a', 'x', 1) == 'xbcabc'
assert 'abc'.find('b', 2) == -1
assert 'abcabc'.rfind('b') == 4
assert 'abcabc'.index('b') == 1
assert 'abcabc'.rindex('b') == 4
assert 'aXbXc'.partition('X') == ('a', 'X', 'bXc')
assert 'aXbXc'.rpartition('X') == ('aXb', 'X', 'c')
assert 'a'.center(5, '*') == '**a**'
assert 'a'.ljust(3, '-') == 'a--'
assert 'a'.rjust(3, '-') == '--a'
assert '42'.zfill(5) == '00042'
assert '-42'.zfill(5) == '-0042'
assert 'a\tb'.expandtabs(4) == 'a   b'
assert 'abc'.removeprefix('ab') == 'c'
assert 'abc'.removesuffix('bc') == 'a'

print("str method error message regression passed")
