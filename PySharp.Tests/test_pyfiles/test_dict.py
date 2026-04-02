"""
Standard dictionary operation and method tests
"""

# Dictionary initialization and indexing
d = {'a': 1, 'b': 2}
assert d['a'] == 1
assert d['b'] == 2

# Adding or updating elements
d['c'] = 3
assert d['c'] == 3
assert len(d) == 3

# Using get() for safe access
assert d.get('a') == 1
assert d.get('x') is None
assert d.get('x', 42) == 42

# setdefault behavior
v = d.setdefault('d', 4)
assert v == 4
assert d['d'] == 4

v = d.setdefault('a', 99)
assert v == 1
assert d['a'] == 1

# Removing elements
v = d.pop('d')
assert v == 4
assert 'd' not in d

try:
    d.pop('missing')
    assert False, "KeyError should be raised"
except KeyError:
    pass

# update behavior
d.update({'a': 100, 'e': 5})
assert d['a'] == 100
assert d['e'] == 5

# items behavior
for k, v in d.items():
    assert k in d
    assert d[k] == v

# clear and copy
d2 = d.copy()
assert 'a' in d2
assert d2 is not d

d.clear()
assert len(d) == 0

print("test_dict passed")
