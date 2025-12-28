d = {'a': 1, 'b': 2}
assert d['a'] == 1
d['c'] = 3
assert d['c'] == 3
assert set(d.keys()) == {'a', 'b', 'c'}
assert set(d.values()) == {1, 2, 3}
assert set(d.items()) == {('a', 1), ('b', 2), ('c', 3)}
assert d.get('a') == 1
assert d.get('x', 42) == 42
d.setdefault('d', 4)
assert d['d'] == 4
v = d.pop('d')
assert v == 4
try:
    d.pop('d')
    assert False
except KeyError:
    pass
k, v = d.popitem()
assert k in {'a', 'b', 'c'}
d.clear()
assert d == {}
d = {'x': 1}
d2 = d.copy()
assert d2 == d and d2 is not d
