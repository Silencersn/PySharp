"""
Regression: the str methods whose sub/old/new arguments go through
CPython's argument converter render None as "None" instead of its type
name "NoneType". CPython's getargs layer special-cases Py_None when it
formats a rejected argument (Python/getargs.c converterr() and
_PyArg_BadArgument(), both `arg == Py_None ? "None" : ...->tp_name`),
while the neighbouring %T faces keep printing "NoneType" — the split is
by message site, not by method family.

CPython 3.14 reference (Python/getargs.c, Objects/unicodeobject.c
do_find/do_count/do_replace, Objects/bytesobject.c bytes_decode
argument clinic).
"""


def msg(fn):
    try:
        fn()
        return "ok"
    except TypeError as e:
        return "TypeError: " + str(e)
    except Exception as e:
        return type(e).__name__ + ": " + str(e)


# --- converterr faces: None is spelled "None" ---

assert msg(lambda: 'abc'.find(None)) == "TypeError: find() argument 1 must be str, not None"
assert msg(lambda: 'abc'.rfind(None)) == "TypeError: rfind() argument 1 must be str, not None"
assert msg(lambda: 'abc'.index(None)) == "TypeError: index() argument 1 must be str, not None"
assert msg(lambda: 'abc'.rindex(None)) == "TypeError: rindex() argument 1 must be str, not None"
assert msg(lambda: 'abc'.count(None)) == "TypeError: count() argument 1 must be str, not None"
assert msg(lambda: 'abc'.replace(None, 'x')) == "TypeError: replace() argument 1 must be str, not None"
assert msg(lambda: 'abc'.replace('x', None)) == "TypeError: replace() argument 2 must be str, not None"
# encode's two keyword-capable arguments take the same path
assert msg(lambda: 'abc'.encode(None)) == "TypeError: encode() argument 'encoding' must be str, not None"
assert msg(lambda: 'abc'.encode('utf-8', None)) == "TypeError: encode() argument 'errors' must be str, not None"

# --- every other type keeps its tp_name: only None is special ---

for value in (1, 1.5, [], {}, (), True, complex(1, 2), b'a', bytearray(b'a'), range(3)):
    assert msg(lambda v=value: 'abc'.find(v)) == (
        "TypeError: find() argument 1 must be str, not " + type(value).__name__
    ), repr(value)

# the sub check still precedes the start/end checks
assert msg(lambda: 'abc'.find(None, b'0')) == "TypeError: find() argument 1 must be str, not None"
assert msg(lambda: 'abc'.find('a', b'0')) == "TypeError: slice indices must be integers or None or have an __index__ method"

# str subclasses are still accepted (PyUnicode_Check), and the happy
# paths are untouched
class S(str):
    pass

assert 'abc'.find(S('b')) == 1
assert 'abc'.find('b') == 1
assert 'abc'.rfind('b') == 1
assert 'abc'.index('b') == 1
assert 'abc'.rindex('b') == 1
assert 'abc'.count('b') == 1
assert 'abc'.replace('b', 'x') == 'axc'
assert 'abc'.encode('utf-8') == b'abc'

# --- %T faces: None still prints NoneType, do not "fix" these ---

assert msg(lambda: 'abc'.startswith(None)) == "TypeError: startswith first arg must be str or a tuple of str, not NoneType"
assert msg(lambda: 'abc'.endswith(None)) == "TypeError: endswith first arg must be str or a tuple of str, not NoneType"
assert msg(lambda: ','.join([None])) == "TypeError: sequence item 0: expected str instance, NoneType found"
assert msg(lambda: 'abc'.partition(None)) == "TypeError: must be str, not NoneType"
assert msg(lambda: 'abc'.rpartition(None)) == "TypeError: must be str, not NoneType"
assert msg(lambda: 'a'.center(5, None)) == "TypeError: The fill character must be a unicode character, not NoneType"
assert msg(lambda: 'a'.ljust(5, None)) == "TypeError: The fill character must be a unicode character, not NoneType"
assert msg(lambda: 'a'.rjust(5, None)) == "TypeError: The fill character must be a unicode character, not NoneType"
assert msg(lambda: 'a'.zfill(None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert msg(lambda: 'a'.center(None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert msg(lambda: 'a,b'.split(',', None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
assert msg(lambda: 'abc'.replace('a', 'x', None)) == "TypeError: 'NoneType' object cannot be interpreted as an integer"
# maxsplit/width/sep accept None as "no limit"/"whitespace", no error at all
assert 'a b'.split(None) == ['a', 'b']
assert 'a b'.split(None, 1) == ['a', 'b']
assert 'a b c'.rsplit(None, 1) == ['a b', 'c']
assert 'a'.center(0) == 'a'

print("str None argument message regression passed")
