"""
Tests for frozenset
"""

s = frozenset([1, 2, 3])
assert type(s) is frozenset
assert len(s) == 3
assert 1 in s

s2 = frozenset([3, 4, 5])

# Operations
assert s.intersection(s2) == frozenset([3])
assert s & s2 == frozenset([3])

assert s.union(s2) == frozenset([1, 2, 3, 4, 5])
assert s | s2 == frozenset([1, 2, 3, 4, 5])

assert s.difference(s2) == frozenset([1, 2])
assert s - s2 == frozenset([1, 2])

assert s.symmetric_difference(s2) == frozenset([1, 2, 4, 5])
assert s ^ s2 == frozenset([1, 2, 4, 5])

# Comparisons
assert frozenset([1, 2]).issubset(s) is True
assert (frozenset([1, 2]) <= s) is True
assert (frozenset([1, 2]) < s) is True

assert s.issuperset(frozenset([1, 2])) is True
assert (s >= frozenset([1, 2])) is True
assert (s > frozenset([1, 2])) is True

assert s.isdisjoint(frozenset([4, 5])) is True

# Other methods
assert s.copy() is s

print("test_frozenset passed")
