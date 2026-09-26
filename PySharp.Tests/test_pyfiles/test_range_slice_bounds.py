"""
slicing a range keeps the start/stop/step computed from
slice.indices() even when the result is empty or reversed. CPython's
compute_slice (Objects/rangeobject.c) builds substart/substop/substep
unconditionally and hands them to range_new — only the length comes out
as zero, the bounds are never collapsed. PySharp used to replace the
bounds with (start, start, step) whenever the length was zero, so every
empty slice printed as range(0, 0).

Only the repr and the preserved bounds change: len(), iteration,
membership and equality were already correct and stay correct.

CPython 3.14 reference (Objects/rangeobject.c compute_slice,
Objects/sliceobject.c _PySlice_GetLongIndices).

:kind: test
"""


def r(value):
    return repr(value)


# --- the reported cases: empty or reversed slices keep their bounds ---

assert r(range(10)[5:2]) == "range(5, 2)"
assert r(range(0, 20, 3)[4:2]) == "range(12, 6, 3)"
assert r(range(10)[100:200]) == "range(10, 10)"
assert r(range(10)[3:7:-1]) == "range(3, 7, -1)"
assert r(range(10)[9:1]) == "range(9, 1)"
assert r(range(0, 10, 3)[2:1]) == "range(6, 3, 3)"

# --- empty slices of every shape ---

assert r(range(10)[5:5]) == "range(5, 5)"
assert r(range(0, 10, 3)[1:1]) == "range(3, 3, 3)"
assert r(range(10)[:0]) == "range(0, 0)"
assert r(range(10)[100:]) == "range(10, 10)"
assert r(range(0)[0:0]) == "range(0, 0)"
assert r(range(10)[-1:-5]) == "range(9, 5)"
# an empty range slices to itself
assert r(range(0)[:]) == "range(0, 0)"

# --- non-empty slices are unchanged ---

assert r(range(10)[2:5]) == "range(2, 5)"
assert r(range(10)[:]) == "range(0, 10)"
assert r(range(10)[::2]) == "range(0, 10, 2)"
assert r(range(10)[::-1]) == "range(9, -1, -1)"
assert r(range(10)[-3:-1]) == "range(7, 9)"
assert r(range(10, 0, -1)[2:8]) == "range(8, 2, -1)"
assert r(range(0, -10, -1)[2:5]) == "range(-2, -5, -1)"
assert r(range(0, 20, 3)[1:4]) == "range(3, 12, 3)"
assert r(range(10)[2:100]) == "range(2, 10)"

# --- slicing an already-empty slice still reports the bounds ---

assert r(range(10)[5:2][1:3]) == "range(5, 5)"
assert r(range(10)[::-1][5:2]) == "range(4, 7, -1)"

# --- a huge range: the bounds keep full precision ---

assert r(range(2**70)[1:0]) == "range(1, 0)"
assert len(range(2**70)[1:0]) == 0

# --- behaviour other than repr is untouched ---

assert len(range(10)[5:2]) == 0
assert len(range(0, 20, 3)[4:2]) == 0
assert len(range(10)[2:5]) == 3
assert list(range(10)[5:2]) == []
assert list(range(0, 20, 3)[4:2]) == []
assert list(range(10)[2:5]) == [2, 3, 4]
assert list(range(10)[::-1]) == [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
assert list(range(0, 20, 3)[1:4]) == [3, 6, 9]
# truthiness and membership follow the length, not the bounds
assert not range(10)[5:2]
assert bool(range(10)[2:5])
assert 5 not in range(10)[5:2]
assert 3 in range(10)[2:5]
# equality compares by contents, so a kept-bound empty range still equals
# any other empty range
assert range(10)[5:2] == range(0, 0)
assert range(10)[5:2] == range(5, 2)
assert range(10)[2:5] == range(2, 5)
assert range(10)[3:7:-1] == range(0, 0)

# --- str() and the tuple of the slice agree with repr ---

assert str(range(10)[5:2]) == "range(5, 2)"
assert str(range(0, 20, 3)[4:2]) == "range(12, 6, 3)"

print("range slice bounds passed")
