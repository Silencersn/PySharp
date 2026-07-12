"""
Tests for range edge cases - covers PyRangeObject iteration with step > 1, negative step
"""

# Range with step > 1
assert list(range(0, 10, 3)) == [0, 3, 6, 9]
assert list(range(0, 5, 2)) == [0, 2, 4]

# Range with negative step
assert list(range(5, 0, -1)) == [5, 4, 3, 2, 1]
assert list(range(10, 0, -3)) == [10, 7, 4, 1]
assert list(range(0, -10, -2)) == [0, -2, -4, -6, -8]

# Range length
assert len(range(0, 10, 3)) == 4
assert len(range(5, 0, -1)) == 5
assert len(range(10, 0, -3)) == 4

# Range contains
assert 3 in range(0, 10, 3)
assert 4 not in range(0, 10, 3)
assert 5 in range(5, 0, -1)
assert 0 not in range(5, 0, -1)

# Range index
# TODO: range.index() not yet implemented
# assert range(0, 10, 3).index(3) == 1
# assert range(0, 10, 3).index(9) == 3
#
# # range.index raises ValueError
# try:
#     range(0, 10).index(99)
#     assert False, "Should raise ValueError"
# except ValueError:
#     pass

# Range with single argument (stop)
assert list(range(5)) == [0, 1, 2, 3, 4]
assert list(range(0)) == []

# Range with start and stop
assert list(range(2, 6)) == [2, 3, 4, 5]

# Empty ranges
assert list(range(0, -5, 1)) == []
assert list(range(5, 0, 1)) == []
assert list(range(0)) == []

# range.__reversed__
assert list(reversed(range(5))) == [4, 3, 2, 1, 0]
assert list(reversed(range(0, 10, 2))) == [8, 6, 4, 2, 0]
assert list(reversed(range(5, 0, -1))) == [1, 2, 3, 4, 5]
assert list(reversed(range(0))) == []

# range.__getitem__ (index access)
assert range(10)[5] == 5
assert range(10)[-1] == 9
assert range(0, 10, 2)[3] == 6
assert range(5, 0, -1)[2] == 3

# range.__getitem__ out of range
try:
    range(10)[100]
    assert False, "Should raise IndexError"
except IndexError:
    pass

try:
    range(10)[-100]
    assert False, "Should raise IndexError"
except IndexError:
    pass

# range.__getitem__ with slice
assert list(range(10)[2:5]) == [2, 3, 4]
assert list(range(10)[:3]) == [0, 1, 2]
assert list(range(10)[5:]) == [5, 6, 7, 8, 9]
assert list(range(10)[::2]) == [0, 2, 4, 6, 8]
assert list(range(10)[::-1]) == [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
assert list(range(0, 10, 2)[1:4]) == [2, 4, 6]
assert list(range(10)[-5:-2]) == [5, 6, 7]

print("test_range_extended passed")
