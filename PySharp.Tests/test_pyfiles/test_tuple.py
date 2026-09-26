"""
Standard tuple operations and behavior tests

:kind: test
"""

# Tuple creation and indexing
t = (1, 2, 3)
assert t[0] == 1
assert t[-1] == 3
assert len(t) == 3

# Tuple unpacking
a, b, c = t
assert a == 1 and b == 2 and c == 3

# Nested tuples
nt = (1, (2, 3), 4)
assert nt[1][0] == 2

# Singleton and empty tuple
empty = ()
assert len(empty) == 0
single = (1,)
assert len(single) == 1
assert single[0] == 1

# Tuple immutability
try:
    t[0] = 10
    assert False, "Tuples should be immutable"
except TypeError:
    pass

print("test_tuple passed")
