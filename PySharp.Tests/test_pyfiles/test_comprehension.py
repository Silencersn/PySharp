lst = [x * x for x in range(5)]
assert lst == [0, 1, 4, 9, 16]

s = {x % 3 for x in range(7)}
for v in [0, 1, 2]:
    assert v in s

d = {x: x * 2 for x in range(4)}
for k, v in [(0, 0), (1, 2), (2, 4), (3, 6)]:
    assert k in d
    assert d[k] == v

nested = [(i, j) for i in range(2) for j in range(2)]
assert nested == [(0, 0), (0, 1), (1, 0), (1, 1)]

filtered = [x for x in range(10) if x % 2 == 0]
assert filtered == [0, 2, 4, 6, 8]

mapped = [x if x % 2 == 0 else -x for x in range(5)]
assert mapped == [0, -1, 2, -3, 4]

def f(x):
    return x * 10
func_map = [f(x) for x in range(3)]
assert func_map == [0, 10, 20]

chars = [c for c in 'abc']
assert chars == ['a', 'b', 'c']

first_five = [x for x in range(1, 6)]
assert first_five == [1, 2, 3, 4, 5]

matrix = [[i * j for j in range(3)] for i in range(3)]
assert matrix == [[0, 0, 0], [0, 1, 2], [0, 2, 4]]

x = 100
scoped = [x for x in range(3)]
assert scoped == [0, 1, 2]
assert x == 100

gen = (x * 2 for x in range(4))
assert list(gen) == [0, 2, 4, 6]

dgen = ((k, v) for k, v in d.items())
assert list(dgen) == [(0, 0), (1, 2), (2, 4), (3, 6)]
