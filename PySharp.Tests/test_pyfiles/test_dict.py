d = {'a': 1, 'b': 2}
assert d['a'] == 1
d['c'] = 3
assert d['c'] == 3
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
d = {'x': 1}
d2 = d.copy()
assert d2 is not d
