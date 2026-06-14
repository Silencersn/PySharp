"""
Tests for tuple extended operations - covers PyTupleObject.Py.cs (add, mul, slice, index, count)
"""

# Tuple addition
assert (1, 2) + (3, 4) == (1, 2, 3, 4)

# Empty tuple addition
assert (1, 2) + () == (1, 2)
assert () + (1, 2) == (1, 2)

# Tuple multiplication
assert (1, 2) * 3 == (1, 2, 1, 2, 1, 2)
assert (1, 2) * 0 == ()
assert (1, 2) * 1 == (1, 2)

# Tuple slicing
t = (0, 1, 2, 3, 4, 5)
assert t[1:4] == (1, 2, 3)
assert t[:3] == (0, 1, 2)
assert t[3:] == (3, 4, 5)
assert t[::2] == (0, 2, 4)
assert t[::-1] == (5, 4, 3, 2, 1, 0)
assert t[1:5:2] == (1, 3)

# Tuple index
assert (10, 20, 30, 20, 10).index(20) == 1
assert (10, 20, 30).index(10) == 0
assert (10, 20, 30).index(30) == 2

# tuple.index with start/end
t = (1, 2, 3, 2, 1)
assert t.index(2, 2) == 3
assert t.index(1, 1, 4) == 4

# Tuple count
assert (1, 2, 2, 3, 2, 1).count(2) == 3
assert (1, 2, 3).count(5) == 0

# Tuple comparison
assert (1, 2) < (1, 3)
assert (1, 2) == (1, 2)
assert (3, 1) > (2, 5)
assert (1, 2, 3) > (1, 2)

# tuple.index raises ValueError
try:
    (1, 2, 3).index(99)
    assert False, "Should raise ValueError"
except ValueError:
    pass

print("test_tuple_extended passed")
