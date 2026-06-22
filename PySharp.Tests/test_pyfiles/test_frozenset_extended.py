"""
Extended tests for frozenset - covers operations on PyFrozenSetObject, PyFrozenSetObjectType
"""

# frozenset basic
fs = frozenset([1, 2, 3, 2, 1])
assert len(fs) == 3
assert isinstance(fs, frozenset)

# frozenset from various iterables
assert frozenset() == frozenset()
assert frozenset([]) == frozenset()
assert frozenset("abc") == frozenset({'a', 'b', 'c'})
assert frozenset(range(3)) == frozenset({0, 1, 2})

# frozenset contains
fs = frozenset([1, 2, 3])
assert 1 in fs
assert 4 not in fs

# frozenset equality
assert frozenset([1, 2, 3]) == frozenset([3, 2, 1])
assert frozenset([1, 2]) != frozenset([1, 2, 3])
assert frozenset() == frozenset()

# frozenset comparison with set
assert frozenset([1, 2]) == {1, 2}
assert {1, 2} == frozenset([1, 2])

# frozenset union
fs1 = frozenset([1, 2])
fs2 = frozenset([2, 3])
result = fs1 | fs2
assert result == frozenset([1, 2, 3])
assert isinstance(result, frozenset)

# frozenset union with set
result = fs1 | {3, 4}
assert result == frozenset([1, 2, 3, 4])
assert isinstance(result, frozenset)

# frozenset union with multiple
result = fs1.union([2, 3], [4, 5])
assert result == frozenset([1, 2, 3, 4, 5])

# frozenset intersection
result = fs1 & fs2
assert result == frozenset([2])

result = fs1.intersection([2, 3, 4])
assert result == frozenset([2])

# frozenset difference
result = fs1 - fs2
assert result == frozenset([1])

result = fs1.difference([2, 3])
assert result == frozenset([1])

# frozenset symmetric_difference
result = fs1 ^ fs2
assert result == frozenset([1, 3])

result = fs1.symmetric_difference([2, 3])
assert result == frozenset([1, 3])

# frozenset issubset
assert frozenset([1, 2]).issubset(frozenset([1, 2, 3])) is True
assert frozenset([1, 2]).issubset(frozenset([1, 2])) is True
assert frozenset([1, 2]).issubset(frozenset([3, 4])) is False
assert frozenset().issubset(frozenset([1])) is True

# frozenset issuperset
assert frozenset([1, 2, 3]).issuperset(frozenset([1, 2])) is True
assert frozenset([1, 2]).issuperset(frozenset([1, 2])) is True
assert frozenset([1]).issuperset(frozenset([2])) is False
assert frozenset().issuperset(frozenset()) is True

# frozenset isdisjoint
assert frozenset([1, 2]).isdisjoint(frozenset([3, 4])) is True
assert frozenset([1, 2]).isdisjoint(frozenset([2, 3])) is False
assert frozenset().isdisjoint(frozenset([1])) is True

# frozenset copy
fs = frozenset([1, 2, 3])
fs_copy = fs.copy()
assert fs_copy == fs
assert isinstance(fs_copy, frozenset)

# frozenset hashable (can be used as dict key or set member)
d = {frozenset([1, 2]): "value1", frozenset([3, 4]): "value2"}
assert d[frozenset([1, 2])] == "value1"
assert d[frozenset([3, 4])] == "value2"

# frozenset as set member
s = {frozenset([1]), frozenset([2]), frozenset([1])}
assert len(s) == 2

# frozenset iteration
fs = frozenset([3, 1, 2])
result = sorted(fs)
assert result == [1, 2, 3]

# frozenset len
assert len(frozenset()) == 0
assert len(frozenset([1])) == 1
assert len(frozenset(range(10))) == 10

# frozenset bool
assert bool(frozenset()) is False
assert bool(frozenset([1])) is True

# frozenset repr
r = repr(frozenset())
assert r == "frozenset()"

r = repr(frozenset([1]))
assert "frozenset({1})" in r.replace(" ", "")

# frozenset with different types
# Note: True == 1 in Python, so both are deduplicated
fs = frozenset([1, "hello", 3.14, None, True])
assert len(fs) == 4
assert 1 in fs
assert "hello" in fs

# frozenset | set returns frozenset
result = frozenset([1, 2]) | {2, 3, 4}
assert isinstance(result, frozenset)
assert result == frozenset([1, 2, 3, 4])

# frozenset & set returns frozenset
result = frozenset([1, 2, 3]) & {2, 3, 4}
assert isinstance(result, frozenset)
assert result == frozenset([2, 3])

# frozenset - set returns frozenset
result = frozenset([1, 2, 3]) - {2, 3}
assert isinstance(result, frozenset)
assert result == frozenset([1])

# frozenset ^ set returns frozenset
result = frozenset([1, 2]) ^ {2, 3}
assert isinstance(result, frozenset)
assert result == frozenset([1, 3])

print("test_frozenset_extended passed")
