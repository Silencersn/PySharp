# Regression: tuple() and str() construct without arguments — the
# zero-arg forms return the empty tuple and the empty string like the
# other containers, the constructor dispatch reports CPython's exact
# arity/keyword faces, and str grows the bytes-decoding form
# str(bytes_like, encoding, errors).

faces = [
    # the six container no-arg constructions
    ("tuple()", "()"),
    ("str()", "''"),
    ("dict()", "{}"),
    ("list()", "[]"),
    ("set()", "set()"),
    ("frozenset()", "frozenset()"),
    # str conversion paths
    ("str('a')", "'a'"),
    ("str(1.5)", "'1.5'"),
    ("str(b'ab')", '''"b'ab'"'''),
    ("str([1, 2])", "'[1, 2]'"),
    ("str(object=1)", "'1'"),
    # tuple arity and keyword faces
    ("tuple(1, 2)", "TypeError: tuple expected at most 1 argument, got 2"),
    ("tuple(1, 2, 3)", "TypeError: tuple expected at most 1 argument, got 3"),
    ("tuple(iterable=[1, 2])", "TypeError: tuple() takes no keyword arguments"),
    ("tuple(1)", "TypeError: 'int' object is not iterable"),
    # str arity, keyword and converter faces
    ("str(1, 2, 3, 4)", "TypeError: str expected at most 3 arguments, got 4"),
    ("str(b'a', bogus=1)", "TypeError: str() got an unexpected keyword argument 'bogus'"),
    ("str(b'a', 'utf-8', encoding='x')", "TypeError: argument for str() given by name ('encoding') and position (2)"),
    ("str(b'a', object=1)", "TypeError: argument for str() given by name ('object') and position (1)"),
    ("str(1, 2)", "TypeError: str() argument 'encoding' must be str, not int"),
    ("str(b'a', None)", "TypeError: str() argument 'encoding' must be str, not NoneType"),
    ("str(b'a', 'utf-8', 1)", "TypeError: str() argument 'errors' must be str, not int"),
    ("str(1, 'utf-8')", "TypeError: decoding to str: need a bytes-like object, int found"),
    ("str('a', 'utf-8')", "TypeError: decoding str is not supported"),
    ("str(b'a', 'bogus-codec')", "LookupError: unknown encoding: bogus-codec"),
    # the decoding form
    ("str(b'a', 'utf-8')", "'a'"),
    ("str(bytearray(b'a'), 'latin-1')", "'a'"),
    ("str(memoryview(b'ab'), 'utf-8')", "'ab'"),
    ("str(b'a', errors='strict')", "'a'"),
    ("str(encoding='utf-8')", "''"),
    ("str(b'a\\x00', 'utf-16-le')", "'a'"),
    # bytes.decode shares the core: unknown codecs are LookupError now
    ("b'a'.decode('utf-8')", "'a'"),
    ("b'a'.decode('bogus')", "LookupError: unknown encoding: bogus"),
]

for expr, expected in faces:
    try:
        got = repr(eval(expr))
    except Exception as e:
        got = type(e).__name__ + ": " + str(e)
    assert got == expected, (expr, got, expected)

# empty constructions carry the right type and content
assert tuple() == ()
assert str() == ""
assert len(tuple()) == 0
assert type(tuple()).__name__ == "tuple"
assert type(str()).__name__ == "str"

print("constructor faces ok,", len(faces), "cases")
print("test_tuple_str_noarg_regression passed")
