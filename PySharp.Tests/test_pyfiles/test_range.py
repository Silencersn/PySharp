"""
Range object tests

:kind: test
"""

r = range(5)
assert list(r) == [0, 1, 2, 3, 4]

r2 = range(1, 6, 2)
assert list(r2) == [1, 3, 5]

r3 = range(5, 0, -1)
assert list(r3) == [5, 4, 3, 2, 1]

# Containment
assert 3 in r
assert 6 not in r

print("test_range passed")
