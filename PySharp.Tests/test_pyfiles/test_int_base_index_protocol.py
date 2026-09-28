"""int(s, base) resolves the base argument through __index__, so any index-capable object is accepted wherever an int works.

The base is converted exactly like CPython long_new: __index__ is consulted,
plain objects without the protocol raise TypeError, int subclasses (bool
included) take the fast path, and a value outside 2..36 (or nonzero) raises
ValueError no matter how large the protocol result was.

:background: CPython 3.14 Objects/longobject.c long_new_impl converts the
    base with PyNumber_AsSsize_t (Objects/abstract.c), which calls
    _PyNumber_Index first; an oversized result saturates to the ssize_t
    bounds and then fails the 2..36 range check as ValueError.
:kind: test
"""

BASE_MSG = "int() base must be >= 2 and <= 36, or 0"


class Ix:
    def __init__(self, value):
        self.value = value

    def __index__(self):
        return self.value


class Plain:
    pass


def expect_base_error(base, message=BASE_MSG):
    try:
        int("1", base)
    except ValueError as e:
        assert str(e) == message, (base, str(e))
    else:
        raise AssertionError("expected ValueError for base " + repr(base))


def expect_type_error(base):
    try:
        int("1", base)
    except TypeError as e:
        return e
    raise AssertionError("expected TypeError for base " + repr(base))


# __index__ objects are accepted as the base
assert int("1010", Ix(2)) == 10
assert int("ff", Ix(16)) == 255
assert int("777", Ix(8)) == 511
assert int("-z", Ix(36)) == -35

# out-of-range protocol results keep failing the range check, including the
# saturating huge case (CPython clips to ssize_t bounds, then rejects)
expect_base_error(Ix(99))
expect_base_error(Ix(-5))
expect_base_error(Ix(10 ** 100))
expect_base_error(Ix(-(10 ** 100)))
expect_base_error(True)  # bool is an int: base 1

# objects without __index__ raise TypeError with the index message
e = expect_type_error(Plain())
assert str(e) == "'Plain' object cannot be interpreted as an integer", str(e)
expect_type_error("16")
expect_type_error(16.0)

# __index__ returning a non-int result is a TypeError
class BadIx:
    def __index__(self):
        return "3"

expect_type_error(BadIx())


# exact int subclasses stay on the fast path
class MyInt(int):
    pass

assert int("1010", MyInt(2)) == 10
expect_base_error(MyInt(1))

# string parsing itself is untouched
assert int("1010", 2) == 10
assert int("0x10", 0) == 16

print("test_int_base_index_protocol passed")
