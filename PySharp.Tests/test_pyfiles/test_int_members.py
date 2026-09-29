"""The int member surface: bit_length, bit_count, to_bytes, from_bytes,
as_integer_ratio, conjugate and the real/imag/numerator/denominator
properties, byte-exact and message-exact against CPython 3.14.

int carries only __new__ on the C side plus slots for the dunders, so every
plain method and getset has to be implemented here explicitly.

:kind: test
:background: CPython 3.14 removed the 'sys' spelling of the byteorder
    argument from int.to_bytes/int.from_bytes (Objects/longobject.c accepts
    only 'big' and 'little' and defers to sys.byteorder for native order);
    int.conjugate, int.as_integer_ratio and the real/imag/numerator/
    denominator getters all funnel through long_long, which converts bool
    and int subclasses to the exact built-in int, so True.numerator is 1
    and not True.
"""


def expect_error(exc_type, message, fn, *args, **kwargs):
    try:
        fn(*args, **kwargs)
    except exc_type as e:
        assert str(e) == message, repr(str(e)) + " != " + repr(message)
    else:
        raise AssertionError("no " + exc_type.__name__)


def expect_type_error(message, fn, *args, **kwargs):
    expect_error(TypeError, message, fn, *args, **kwargs)


def expect_value_error(message, fn, *args, **kwargs):
    expect_error(ValueError, message, fn, *args, **kwargs)


def expect_overflow_error(message, fn, *args, **kwargs):
    expect_error(OverflowError, message, fn, *args, **kwargs)


def expect_argument_error(fn, *args, **kwargs):
    # the arity-rejection wording differs between interpreters; only the
    # rejection itself is pinned here
    try:
        fn(*args, **kwargs)
    except TypeError:
        pass
    else:
        raise AssertionError("no TypeError")


# --- bit_length / bit_count ---

assert (0).bit_length() == 0
assert (255).bit_length() == 8
assert (-255).bit_length() == 8  # of the absolute value
assert (10**100).bit_length() == 333
assert (0).bit_count() == 0
assert (255).bit_count() == 8
assert (-255).bit_count() == 8  # of the absolute value
assert (10**100).bit_count() == 105

expect_argument_error(lambda: (5).bit_length(1))
expect_argument_error(lambda: (5).bit_count(1))

# --- conjugate / as_integer_ratio and the rational protocol ---

assert (5).conjugate() == 5
assert (-5).conjugate() == -5
assert (5).as_integer_ratio() == (5, 1)
assert (-5).as_integer_ratio() == (-5, 1)
assert (0).as_integer_ratio() == (0, 1)
assert (10**100).as_integer_ratio()[1] == 1

# long_long funnels bool and int subclasses to the exact built-in int
assert type(True.conjugate()).__name__ == "int"
assert type(True.numerator).__name__ == "int"
assert (True.numerator, True.denominator) == (1, 1)
assert (True.real, True.imag) == (1, 0)
assert (False.denominator, False.imag) == (1, 0)


class MyInt(int):
    pass


assert type(MyInt(3).conjugate()).__name__ == "int"
assert MyInt(3).as_integer_ratio() == (3, 1)
assert type(MyInt(3).numerator).__name__ == "int"

expect_argument_error(lambda: (5).conjugate(1))
expect_argument_error(lambda: (5).as_integer_ratio(1))

# --- to_bytes ---

assert (5).to_bytes() == b"\x05"
assert (256).to_bytes(2) == b"\x01\x00"
assert (256).to_bytes(2, "little") == b"\x00\x01"
assert (65536).to_bytes(4, "big") == b"\x00\x01\x00\x00"
assert (0).to_bytes(0) == b""
assert (0).to_bytes(1) == b"\x00"
assert (-5).to_bytes(2, signed=True) == b"\xff\xfb"
assert (-1).to_bytes(2, "little", signed=True) == b"\xff\xff"
assert (-128).to_bytes(1, signed=True) == b"\x80"
assert (-256).to_bytes(2, signed=True) == b"\xff\x00"
assert (5).to_bytes(length=2, byteorder="big", signed=False) == b"\x00\x05"
assert (5).to_bytes(1, signed="yes") == b"\x05"  # bool via truth value
assert (5).to_bytes(True) == b"\x05"  # length via __index__
assert (2**100 - 1).to_bytes(13, "big") == b"\x0f" + b"\xff" * 12

expect_overflow_error("int too big to convert", lambda: (256).to_bytes(1))
expect_overflow_error("int too big to convert", lambda: (5).to_bytes(0))
expect_overflow_error("int too big to convert", lambda: (-256).to_bytes(1, signed=True))
expect_overflow_error("int too big to convert", lambda: (-129).to_bytes(1, signed=True))
expect_overflow_error("can't convert negative int to unsigned", lambda: (-5).to_bytes(2))
expect_overflow_error("can't convert negative int to unsigned", lambda: (-5).to_bytes(0))
expect_value_error("byteorder must be either 'little' or 'big'", lambda: (5).to_bytes(1, "middle"))
expect_value_error("byteorder must be either 'little' or 'big'", lambda: (5).to_bytes(-1, "middle"))
expect_value_error("length argument must be non-negative", lambda: (5).to_bytes(-1))
expect_type_error("to_bytes() argument 'byteorder' must be str, not int", lambda: (5).to_bytes(1, 5))
expect_type_error("to_bytes() argument 'byteorder' must be str, not bytes", lambda: (5).to_bytes(1, b"big"))
expect_type_error("'float' object cannot be interpreted as an integer", lambda: (5).to_bytes(1.5))

# --- from_bytes ---

assert int.from_bytes(b"\x01\x00") == 256
assert int.from_bytes(b"\x01\x00", "little") == 1
assert int.from_bytes(b"") == 0
assert int.from_bytes(b"\xff", signed=True) == -1
assert int.from_bytes(b"\x80", signed=True) == -128
assert int.from_bytes(b"\xff\xff\xff\xff\xff\xff\xff\xff", signed=True) == -1
assert int.from_bytes(bytearray(b"\x0a")) == 10
assert int.from_bytes(memoryview(b"\x0b")) == 11
assert int.from_bytes([1, 0]) == 256  # iterable of ints
assert int.from_bytes((1, 2)) == 258
assert int.from_bytes(x for x in (1, 2)) == 258
assert int.from_bytes({}) == 0
assert int.from_bytes(bytes=b"\x01", byteorder="big", signed=False) == 1
assert int.from_bytes(b"\x01", signed="yes") == 1
assert int.from_bytes((255).to_bytes(3, signed=True)) == 255
assert int.from_bytes(b"\xff" * 100).bit_length() == 800

# int.from_bytes rebuilds through the receiving type for subclasses
assert bool.from_bytes(b"\x00") is False
assert bool.from_bytes(b"\x01") is True
assert bool.from_bytes(b"\x02") is True

expect_type_error("cannot convert 'str' object to bytes", lambda: int.from_bytes("ab"))
expect_type_error("cannot convert 'int' object to bytes", lambda: int.from_bytes(5))
expect_type_error("cannot convert 'NoneType' object to bytes", lambda: int.from_bytes(None))
expect_type_error("'float' object cannot be interpreted as an integer", lambda: int.from_bytes([1.0]))
expect_value_error("bytes must be in range(0, 256)", lambda: int.from_bytes([300]))
expect_value_error("bytes must be in range(0, 256)", lambda: int.from_bytes([-1]))
expect_value_error("byteorder must be either 'little' or 'big'", lambda: int.from_bytes(b"\x01", "middle"))
expect_type_error("from_bytes() argument 'byteorder' must be str, not int", lambda: int.from_bytes(b"\x01", 5))

# round trips keep the value, the sign and the endianness contract
for value, length, order, signed in [
    (0, 1, "big", False),
    (0, 0, "big", False),
    (255, 1, "big", False),
    (255, 2, "little", False),
    (10**100, 42, "big", False),
    (-1, 1, "big", True),
    (-10**100, 42, "little", True),
    (2**100, 13, "big", True),
]:
    raw = value.to_bytes(length, order, signed=signed)
    assert int.from_bytes(raw, order, signed=signed) == value, (value, order, signed)
    flipped = "little" if order == "big" else "big"
    assert raw != value.to_bytes(length, flipped, signed=signed) or length <= 1

print("test_int_members passed")
