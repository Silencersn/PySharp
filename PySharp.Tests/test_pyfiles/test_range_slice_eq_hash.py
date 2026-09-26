"""
range and slice must compare by their parameters and hash
consistently with that equality. Both fell back to identity semantics:
range(3) == range(3) was False and equal ranges hashed differently, so
they were unusable as dict keys / set elements.

CPython 3.14 reference (range_equals/range_hash, slice_richcompare/
slice_hash): empty ranges are all equal; a single-element range ignores
its step; range hashes (length, start, step) as a tuple with None for
parts that cannot differ; slice equality compares (start, stop, step)
part-wise and slice_hash is the tuple hash of those parts.

:kind: test
"""

# parameter equality
assert range(3) == range(3)
assert range(0, 3, 1) == range(3) and range(3) == range(0, 3, 1)
assert range(0, 5, 7) == range(0, 5, 7)
assert range(3) != range(4)
assert range(3) != range(3, 0)

# empty ranges are equal regardless of parameters (range(2,0,-1) has
# length 2, so it is not part of this special case)
assert range(0) == range(5, 5)
assert range(0) == range(4, 4, 3)

# single-element ranges ignore the step
assert range(2, 3, 999) == range(2, 3, 1)
assert range(0, 5, 7) != range(3, 4, 100)

# hashes agree with equality
assert hash(range(3)) == hash(range(0, 3, 1))
assert hash(range(0)) == hash(range(5, 5))
assert hash(range(2, 3, 999)) == hash(range(2, 3, 1))
assert hash(range(0)) != hash(range(2, 0, -1))

# usable as dict keys and set elements
assert {range(3): 'a'}[range(0, 3)] == 'a'
assert {range(0, 3, 1): 'b'}[range(3)] == 'b'
assert len({range(3), range(0, 3, 1), range(4)}) == 2
assert range(3) in {range(0, 3, 1)}

# non-ranges stay unequal; ordering unsupported
assert range(3) != 5 and not (range(3) == 'x')
try:
    range(3) < range(5)
    assert False, "TypeError expected"
except TypeError:
    pass

# slice parameter equality and consistent hash
assert slice(1, 10) == slice(1, 10)
assert not (slice(1, 2) != slice(1, 2))
assert hash(slice(1, 2)) == hash(slice(1, 2))
assert hash(slice(1, 2, 3)) == hash(slice(1, 2, 3))
assert slice(1, 2) == slice(1, 2, None)
assert slice(None, 3) != slice(0, 3, 1)
assert slice(1, 2) != slice(1, 3)
assert slice(1, 2, True) == slice(1, 2, 1)
assert {slice(1, 2): 's'}[slice(1, 2)] == 's'
assert len({slice(1, 2), slice(1, 2, None)}) == 1
assert not (slice(1, 2) == (1, 2, None))
assert not (slice(1, 2) == 5)

print("test_range_slice_eq_hash passed")
