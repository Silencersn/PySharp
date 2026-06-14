"""
Extended dictionary tests - more operations, edge cases, and error handling
"""

# dict() constructor from various sources
d = dict([('a', 1), ('b', 2)])
assert d['a'] == 1
assert d['b'] == 2

# dict() from mapping
d2 = dict(d)
assert d2['a'] == 1
assert d2['b'] == 2
assert d2 is not d

# dict() with keyword arguments
d3 = dict(x=10, y=20)
assert d3['x'] == 10
assert d3['y'] == 20

# keys(), values(), items()
d = {'a': 1, 'b': 2, 'c': 3}
keys = list(d.keys())
assert 'a' in keys
assert 'b' in keys
assert 'c' in keys

vals = list(d.values())
assert 1 in vals
assert 2 in vals
assert 3 in vals

# popitem()
d = {'a': 1, 'b': 2}
k, v = d.popitem()
assert k in ('a', 'b')

# del operation on dict
d = {'a': 1, 'b': 2, 'c': 3}
del d['b']
assert 'b' not in d
assert len(d) == 2

# del on non-existent key
try:
    del d['nonexistent']
    assert False, "KeyError should be raised"
except KeyError:
    pass

# Non-string keys
d = {}
d[1] = 'int_key'
d[(1, 2)] = 'tuple_key'
d[True] = 'bool_key'
assert d[1] == 'int_key'
assert d[(1, 2)] == 'tuple_key'
assert d[True] == 'bool_key'

# Dict comparison
assert {'a': 1} == {'a': 1}
assert {'a': 1} != {'a': 2}
assert {'a': 1, 'b': 2} == {'b': 2, 'a': 1}

# len() on dict
assert len({'a': 1, 'b': 2, 'c': 3}) == 3
assert len({}) == 0

# 'in' operator on dict
d = {'a': 1, 'b': 2}
assert 'a' in d
assert 'c' not in d

# fromkeys
d = dict.fromkeys(['a', 'b', 'c'], 0)
assert d['a'] == 0
assert d['b'] == 0
assert d['c'] == 0

d = dict.fromkeys(['x', 'y'])
assert d['x'] is None
assert d['y'] is None

# update with iterable of pairs
d = {'a': 1}
d.update([('b', 2), ('c', 3)])
assert d['a'] == 1
assert d['b'] == 2
assert d['c'] == 3

# Clear and empty
d = {'a': 1, 'b': 2}
d.clear()
assert len(d) == 0
assert d == {}

# Nested dict operations
d = {'outer': {'inner': 42}}
assert d['outer']['inner'] == 42

print("test_dict_extended passed")
