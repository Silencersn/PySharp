"""
Regression: bytes(n)/bytearray(n) with an index-able source (int, bool,
any __index__) must zero-fill n bytes like CPython, with CPython's exact
error handling: negative count -> ValueError, ssize_t overflow ->
OverflowError, and all top-level TypeErrors replaced by "cannot convert
'X' object to bytes/bytearray" while item-level errors keep their own
messages.
"""

assert bytes(3) == b"\x00\x00\x00"
assert bytes(0) == b""
assert bytes(True) == b"\x00"

try:
    bytes(-1)
    raise AssertionError("negative count accepted")
except ValueError as e:
    assert str(e) == "negative count", str(e)

# the iterable protocol stays the fallback and keeps working
assert bytes([104, 105]) == b"hi"
assert bytes((1, 2)) == b"\x01\x02"
assert bytes({1: "a", 2: "b"}) == b"\x01\x02"
assert bytes(i for i in [1, 2]) == b"\x01\x02"
assert bytes(bytearray(b"ab")) == b"ab"

# a non-iterable non-indexable source gets CPython's replaced message
try:
    bytes(3.5)
    raise AssertionError("float accepted")
except TypeError as e:
    assert str(e) == "cannot convert 'float' object to bytes", str(e)

# __index__ wins over __iter__
class Both:
    def __index__(self):
        return 2

    def __iter__(self):
        return iter([9, 9, 9])


assert bytes(Both()) == b"\x00\x00"


class Idx:
    def __index__(self):
        return 2


assert bytes(Idx()) == b"\x00\x00"

# a broken __index__ also gets the replaced top-level message
class Bad:
    def __index__(self):
        return "x"


try:
    bytes(Bad())
    raise AssertionError("bad __index__ accepted")
except TypeError as e:
    assert str(e) == "cannot convert 'Bad' object to bytes", str(e)

# index values beyond ssize_t overflow
class Big:
    def __index__(self):
        return 10 ** 30


try:
    bytes(Big())
    raise AssertionError("huge count accepted")
except OverflowError as e:
    assert str(e) == "cannot fit 'Big' into an index-sized integer", str(e)

# non-TypeError errors from __iter__ propagate untouched
class Boom:
    def __iter__(self):
        raise RuntimeError("iter-boom")


try:
    bytes(Boom())
    raise AssertionError("boom iter accepted")
except RuntimeError as e:
    assert str(e) == "iter-boom", str(e)

# item-level errors keep their own messages
try:
    bytes([1, "a"])
    raise AssertionError("bad item accepted")
except TypeError as e:
    assert str(e) == "'str' object cannot be interpreted as an integer", str(e)

# bytearray mirrors the same dispatch with its own message wording
assert bytearray(3) == bytearray(b"\x00\x00\x00")
assert bytearray(0) == bytearray(b"")
assert bytearray(True) == bytearray(b"\x00")

try:
    bytearray(-2)
    raise AssertionError("bytearray negative count accepted")
except ValueError as e:
    assert str(e) == "negative count", str(e)

assert bytearray([104, 105]) == bytearray(b"hi")
assert bytearray({1: "a"}) == bytearray(b"\x01")
assert bytearray(i for i in [1, 2]) == bytearray(b"\x01\x02")
assert bytearray(b"ab") == bytearray(b"ab")
assert bytearray(bytearray(b"x")) == bytearray(b"x")

try:
    bytearray(3.5)
    raise AssertionError("bytearray float accepted")
except TypeError as e:
    assert str(e) == "cannot convert 'float' object to bytearray", str(e)

assert bytearray(Both()) == bytearray(b"\x00\x00")
assert bytearray(Idx()) == bytearray(b"\x00\x00")

try:
    bytearray(Bad())
    raise AssertionError("bytearray bad __index__ accepted")
except TypeError as e:
    assert str(e) == "cannot convert 'Bad' object to bytearray", str(e)

try:
    bytearray(Big())
    raise AssertionError("bytearray huge count accepted")
except OverflowError as e:
    assert str(e) == "cannot fit 'Big' into an index-sized integer", str(e)

try:
    bytearray(Boom())
    raise AssertionError("bytearray boom iter accepted")
except RuntimeError as e:
    assert str(e) == "iter-boom", str(e)

try:
    bytearray([1, "a"])
    raise AssertionError("bytearray bad item accepted")
except TypeError as e:
    assert str(e) == "'str' object cannot be interpreted as an integer", str(e)

print("test_bytes_int_constructor_regression passed")
