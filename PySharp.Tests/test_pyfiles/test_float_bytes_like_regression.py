"""
Regression test: float() must accept bytes-like arguments, matching CPython's
PyFloat_FromString branch order.

CPython 3.14 semantics (verified against 3.14.4):
  - PyUnicode_Check -> the Nd/space transform runs first
  - PyBytes_Check / PyByteArray_Check / PyObject_GetBuffer -> the raw bytes are
    parsed directly, with NO transform (Objects/floatobject.c:198-225)
  - otherwise -> TypeError "float() argument must be a string or a real number"

float_new_impl only routes an *exact* str to PyFloat_FromString; everything
else goes through PyNumber_Float, which consults __float__ before the string
fallback. So a str/bytes/bytearray subclass that defines __float__ wins over
the text parse, and a plain str subclass still reaches the parse tail.
"""

MSG = "could not convert string to float: "
TYPE_MSG = "float() argument must be a string or a real number, not "


def expect_ve(arg):
    try:
        float(arg)
    except ValueError as e:
        assert str(e) == MSG + repr(arg), str(e)
        return
    raise AssertionError("expected ValueError: " + repr(arg))


def expect_te(arg, typename):
    try:
        float(arg)
    except TypeError as e:
        assert str(e) == TYPE_MSG + "'" + typename + "'", str(e)
        return
    raise AssertionError("expected TypeError: " + repr(arg))


# ===== 1. bytes / bytearray / memoryview are parsed =====

assert float(b"1.5") == 1.5
assert float(bytearray(b"2.5")) == 2.5
assert float(memoryview(b"1.5")) == 1.5
assert float(memoryview(bytearray(b"3.5"))) == 3.5

# special values reach the same tail as the str branch
assert float(b"inf") == float("inf")
assert float(b"-inf") == float("-inf")
assert float(b"Infinity") == float("inf")
assert float(b"nan") != float(b"nan")

# whitespace that Py_ISSPACE accepts is stripped on both ends
assert float(b" 1.5 ") == 1.5
assert float(b"\t1.5\r") == 1.5
assert float(b"1.5\n") == 1.5

# underscores are validated and stripped by the shared tail
assert float(b"1_0.5") == 10.5
assert float(b"1_000") == 1000.0

# ===== 2. malformed bytes keep the ValueError with the bytes repr =====

expect_ve(b"0x1")
expect_ve(b"")
expect_ve(b"   ")
expect_ve(b"1,5")
expect_ve(b"1__0")
expect_ve(b"1_")
expect_ve(b"_1")
expect_ve(b"\xff")

# an embedded NUL is rejected, and the repr shows the escape
expect_ve(b"1\x002")

# ===== 3. the bytes path takes no Nd/space transform =====

# U+00A0 and U+0085 are spaces to .NET and reach the str path as ' ', but as
# raw bytes they must fail: PyFloat_FromString does not transform the buffer
expect_ve(b"\xa0 1")
expect_ve(b"1.5\xa0")
expect_ve(b"\x85 1")

# the str path keeps transforming them
assert float("\xa0 1") == 1.0
assert float("\x851") == 1.0
assert float("\uff11") == 1.0     # fullwidth digit goes through Nd transform

# ===== 4. subclasses: __float__ beats the text parse =====

class StrSub(str):
    pass


class StrFloat(str):
    def __float__(self):
        return 42.0


class BytesSub(bytes):
    pass


class BytesFloat(bytes):
    def __float__(self):
        return 7.5


class BArySub(bytearray):
    pass


assert float(StrSub("2.5")) == 2.5
assert float(StrFloat("1.5")) == 42.0     # __float__ wins over the parse
assert float(StrFloat("zz")) == 42.0

assert float(BytesSub(b"1.5")) == 1.5
assert float(BytesFloat(b"1.5")) == 7.5   # __float__ wins over the parse
assert float(BArySub(b"3.5")) == 3.5

# a bytes subclass still parses, and its own repr is quoted on failure
expect_ve(BytesSub(b"0x1"))


class BytesRepr(bytes):
    def __repr__(self):
        return "<BR-custom>"


try:
    float(BytesRepr(b"zz"))
    raise AssertionError("bad bytes subclass accepted")
except ValueError as e:
    assert str(e) == MSG + "<BR-custom>", str(e)

# ===== 5. the shared PyFloat_AsDouble path still rejects bytes =====

# CPython's math/%/round go through PyFloat_AsDouble, which has no string
# fallback: a bytes-like argument is a plain "must be real number" TypeError
try:
    import math

    math.ceil(b"1.5")
    raise AssertionError("math.ceil accepted bytes")
except TypeError as e:
    assert str(e) == "must be real number, not bytes", str(e)

try:
    math.ceil("1.5")
    raise AssertionError("math.ceil accepted str")
except TypeError as e:
    assert str(e) == "must be real number, not str", str(e)

# ===== 6. a released memoryview is not a buffer any more =====

mv = memoryview(b"1.5")
assert float(mv) == 1.5
mv.release()
expect_te(mv, "memoryview")

# ===== 7. neighbouring conversions are unchanged =====

assert int(b"123") == 123
try:
    int(b"1.5")
    raise AssertionError("int accepted a float literal")
except ValueError as e:
    assert str(e) == "invalid literal for int() with base 10: b'1.5'", str(e)

try:
    complex(b"1.5")
    raise AssertionError("complex accepted bytes")
except TypeError as e:
    assert str(e) == "complex() argument must be a string or a number, not bytes", str(e)

assert float(3) == 3.0
assert float(True) == 1.0
assert float() == 0.0
expect_te(None, "NoneType")
expect_te([], "list")

print("test_float_bytes_like_regression passed")
