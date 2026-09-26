"""Verifies slice boundaries go through __index__ (PySlice_Unpack / _PyEval_SliceIndex), raising catchable TypeErrors instead of a .NET InvalidCastException.

Covers __index__-provided bounds across list/tuple/bytes/bytearray/str, exact error messages, zero-step rejection before bounds unpacking, out-of-range saturation, and the shared conversion in slice assignment and deletion.

:kind: test
"""

# slice boundaries must go through __index__ (PySlice_Unpack /
# _PyEval_SliceIndex), raising catchable TypeErrors instead of a .NET
# InvalidCastException.
class Idx:
    def __index__(self): return 2

L = [10, 20, 30, 40]
assert L[0:Idx()] == [10, 20]
assert L[Idx():] == [30, 40]
assert L[:Idx()] == [10, 20]
assert L[::Idx()] == [10, 30]
assert L[slice(0, Idx())] == [10, 20]
assert L[4:Idx():-2] == [40]
assert tuple("abcd")[1:Idx()] == ("b",)

def expect_type_error(key):
    try:
        L[key]
    except TypeError as e:
        assert str(e) == "slice indices must be integers or None or have an __index__ method"
        return
    raise AssertionError("expected TypeError")

for key in [slice("a", 2), slice(0, "a"), slice(0, 2, "a"), slice(0, 1.5), slice(0, object())]:
    expect_type_error(key)

# zero step is rejected before the bounds are unpacked
try:
    L[slice(0, "a", 0)]
except ValueError as e:
    assert str(e) == "slice step cannot be zero"
except TypeError:
    raise AssertionError("ValueError expected before bounds are unpacked")

# a non-int __index__ result surfaces the protocol's own error
class Bad:
    def __index__(self): return "x"

try:
    L[Bad():]
except TypeError as e:
    assert str(e) == "__index__ returned non-int (type str)"

# out-of-range ints saturate before the length clamp
assert L[0:10**100] == [10, 20, 30, 40]
assert L[-10**100:] == [10, 20, 30, 40]
assert L[::10**100] == [10]
assert L[Idx():10**100] == [30, 40]
assert L[0:True] == [10]

# slice assignment and deletion share the conversion
L2 = [1, 2, 3, 4]
L2[0:Idx()] = [9, 8]
assert L2 == [9, 8, 3, 4]
del L2[0:Idx()]
assert L2 == [3, 4]

# bytes/bytearray/str paths share the conversion
assert b"abcd"[1:Idx()] == b"b"
assert bytearray(b"abcd")[1:Idx()] == bytearray(b"b")
assert "abcd"[1:Idx()] == "b"
print("ok")
