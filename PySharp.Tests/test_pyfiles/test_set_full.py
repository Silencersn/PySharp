# Test set operations
s1 = {1, 2, 3}
s2 = {3, 4, 5}

# Basic operations
assert len(s1) == 3
assert 1 in s1
assert 4 not in s1

# union
assert s1.union(s2) == {1, 2, 3, 4, 5}
assert s1 | s2 == {1, 2, 3, 4, 5}

# intersection
assert s1.intersection(s2) == {3}
assert s1 & s2 == {3}

# difference
assert s1.difference(s2) == {1, 2}
assert s1 - s2 == {1, 2}

# symmetric_difference
assert s1.symmetric_difference(s2) == {1, 2, 4, 5}
assert s1 ^ s2 == {1, 2, 4, 5}

# add/remove/discard/pop/clear
s = {1, 2}
s.add(3)
assert s == {1, 2, 3}
s.remove(3)
assert s == {1, 2}
try:
    s.remove(3)
    assert False, "Should raise KeyError"
except KeyError:
    pass
s.discard(3) # Should not raise error
assert s == {1, 2}
val = s.pop()
assert val in {1, 2}
assert len(s) == 1
s.clear()
assert len(s) == 0

# issubset/issuperset/isdisjoint
assert {1, 2}.issubset({1, 2, 3})
assert {1, 2} <= {1, 2, 3}
assert {1, 2} < {1, 2, 3}
assert not ({1, 2, 3} < {1, 2, 3})
assert {1, 2, 3}.issuperset({1, 2})
assert {1, 2, 3} >= {1, 2}
assert {1, 2, 3} > {1, 2}
assert not ({1, 2} > {1, 2})
assert {1, 2}.isdisjoint({3, 4})
assert not {1, 2}.isdisjoint({2, 3})

# update/intersection_update/difference_update/symmetric_difference_update
s = {1, 2}
s.update({2, 3}, {4, 5})
assert s == {1, 2, 3, 4, 5}
s |= {6}
assert s == {1, 2, 3, 4, 5, 6}

s.intersection_update({1, 2, 3})
assert s == {1, 2, 3}
s &= {2, 3, 4}
assert s == {2, 3}

s.difference_update({2})
assert s == {3}
s -= {4}
assert s == {3}

s.symmetric_difference_update({3, 4})
assert s == {4}
s ^= {4, 5}
assert s == {5}

# copy
s = {1, 2, 3}
s_copy = s.copy()
assert s == s_copy
assert s is not s_copy
s.add(4)
assert 4 not in s_copy

# Constructor with iterable
assert set([1, 2, 2, 3]) == {1, 2, 3}
assert set((1, 1, 2)) == {1, 2}

print("Set tests passed!")
