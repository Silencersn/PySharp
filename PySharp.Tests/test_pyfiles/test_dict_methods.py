"""
Tests for dict methods - setdefault, popitem, update edge cases, clear
Exercises PyDictObject.Py.cs, PyDictObjectType
"""

# setdefault - key not present
d = {'a': 1, 'b': 2}
v = d.setdefault('c', 3)
assert v == 3
assert d['c'] == 3

# setdefault - key already present
v = d.setdefault('a', 99)
assert v == 1
assert d['a'] == 1  # unchanged

# setdefault with default=None (implicit)
v = d.setdefault('x')
assert v is None
assert 'x' in d
assert d['x'] is None

# popitem
d = {'a': 1, 'b': 2}
k, v = d.popitem()
assert k not in d  # removed
assert len(d) == 1

# keys() returns a view
d = {'x': 10, 'y': 20}
keys = d.keys()
assert 'x' in keys
assert 'y' in keys
assert 'z' not in keys

# values() returns a view
vals = d.values()
assert 10 in vals
assert 20 in vals
assert 30 not in vals

# items() returns a view
items = d.items()
assert ('x', 10) in items
assert ('y', 20) in items

# update with no args
d = {'a': 1}
d.update()
assert d == {'a': 1}

# Clear and reuse
d = {'a': 1, 'b': 2}
d.clear()
assert len(d) == 0
d['new'] = 42
assert d['new'] == 42

# Copy then modify (original unchanged)
d = {'a': [1, 2], 'b': 3}
d2 = d.copy()
assert d2['b'] == 3
assert d2 is not d

# Nested dict access
d = {'outer': {'inner': {'value': 42}}}
assert d['outer']['inner']['value'] == 42

# Dict membership
d = {'a': 1}
assert 'a' in d
assert 'b' not in d
assert 1 not in d  # keys only, not values

print("test_dict_methods passed")
