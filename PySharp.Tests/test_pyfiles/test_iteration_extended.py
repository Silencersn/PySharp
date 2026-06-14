"""
Extended iteration tests - exercises PyUtils iterable conversion and more iteration patterns
"""

# list() on various iterables
assert list(range(5)) == [0, 1, 2, 3, 4]
assert list("hello") == ['h', 'e', 'l', 'l', 'o']
assert list((1, 2, 3)) == [1, 2, 3]
assert list({1, 2, 3}) == [1, 2, 3] or sorted(list({1, 2, 3})) == [1, 2, 3]

# tuple() on various iterables
assert tuple([1, 2, 3]) == (1, 2, 3)
assert tuple("abc") == ('a', 'b', 'c')
assert tuple(range(3)) == (0, 1, 2)

# set() on various iterables
assert set([1, 2, 3, 2, 1]) == {1, 2, 3}
assert set("abracadabra") == {'a', 'b', 'r', 'c', 'd'}
assert set(range(3)) == {0, 1, 2}

# Iterating over dict
d = {'a': 1, 'b': 2, 'c': 3}
keys = []
for k in d:
    keys.append(k)
assert 'a' in keys
assert 'b' in keys
assert 'c' in keys

# Iterating over dict items
d = {'x': 10, 'y': 20}
for k, v in d.items():
    assert d[k] == v

# Iterating over dict keys
for k in d.keys():
    assert k in d

# Iterating over dict values
for v in d.values():
    assert v in (10, 20)

# Iterating over enumerate
for i, v in enumerate(['a', 'b', 'c']):
    assert v == ['a', 'b', 'c'][i]

# Iterating over zip
for a, b in zip([1, 2, 3], [4, 5, 6]):
    assert b == a + 3

# Nested iteration
matrix = [[1, 2], [3, 4], [5, 6]]
flat = []
for row in matrix:
    for cell in row:
        flat.append(cell)
assert flat == [1, 2, 3, 4, 5, 6]

# Iterating over reversed
for i, v in enumerate(reversed([1, 2, 3])):
    assert v == 3 - i

# Iterating over sorted
prev = -1
for v in sorted([3, 1, 4, 1, 5]):
    assert v >= prev
    prev = v

# list comprehension with multiple loops
pairs = [(x, y) for x in [1, 2] for y in ['a', 'b']]
assert pairs == [(1, 'a'), (1, 'b'), (2, 'a'), (2, 'b')]

# Generator expressions
gen = (x**2 for x in range(5))
assert list(gen) == [0, 1, 4, 9, 16]

# Generator function
def count_up(n):
    i = 0
    while i < n:
        yield i
        i += 1

assert list(count_up(5)) == [0, 1, 2, 3, 4]

# Generator with send
def gen_send():
    val = yield 1
    yield val

g = gen_send()
assert next(g) == 1

# Chain iteration
result = []
for x in [1, 2]:
    for y in [3, 4]:
        result.append((x, y))
assert result == [(1, 3), (1, 4), (2, 3), (2, 4)]

print("test_iteration_extended passed")
