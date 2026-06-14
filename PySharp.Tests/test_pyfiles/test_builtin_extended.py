"""
Extended built-in function tests - more edge cases and uncovered functions
"""

# type() tests
assert type(1) is int
assert type("hello") is str
assert type([1, 2]) is list
assert type((1,)) is tuple
assert type({'a': 1}) is dict

# isinstance() edge cases
assert isinstance(1, int)
assert isinstance(True, int)
assert isinstance(True, bool)
assert isinstance(3.14, (int, float))
assert isinstance("abc", (str, bytes))
assert not isinstance("abc", int)

# issubclass edge cases
assert issubclass(bool, int)
assert issubclass(int, object)
assert issubclass(str, object)

# reversed()
r = reversed([1, 2, 3])
assert list(r) == [3, 2, 1]

r = reversed("hello")
assert list(r) == ['o', 'l', 'l', 'e', 'h']

# enumerate()
e = enumerate(['a', 'b', 'c'])
assert list(e) == [(0, 'a'), (1, 'b'), (2, 'c')]

e = enumerate(['a', 'b', 'c'], start=1)
assert list(e) == [(1, 'a'), (2, 'b'), (3, 'c')]

# zip() edge cases
assert list(zip([1, 2], [3, 4])) == [(1, 3), (2, 4)]
assert list(zip([1], [2], [3])) == [(1, 2, 3)]
assert list(zip([1, 2, 3], [4, 5])) == [(1, 4), (2, 5)]

# map()
m = map(lambda x: x * 2, [1, 2, 3])
assert list(m) == [2, 4, 6]

m = map(lambda x, y: x + y, [1, 2, 3], [4, 5, 6])
assert list(m) == [5, 7, 9]

# filter()
f = filter(lambda x: x > 2, [1, 2, 3, 4, 5])
assert list(f) == [3, 4, 5]

f = filter(None, [0, 1, False, 2, None, 3])
assert list(f) == [1, 2, 3]

# round() edge cases
assert round(3.14159, 2) == 3.14
assert round(3.14159, 0) == 3.0
assert round(3.5) == 4
assert round(2.5) == 2

# locals() and globals() basic
g = globals()
assert '__name__' in g
assert 'g' in g

# hash() edge cases
assert hash(1) == hash(1)
assert hash("hello") == hash("hello")
assert isinstance(hash(42), int)

# id() basic
assert isinstance(id([1, 2, 3]), int)
assert id(1) == id(1)  # small ints may be cached

# bool() edge cases
assert bool(1) is True
assert bool(0) is False
assert bool("") is False
assert bool("hello") is True
assert bool([]) is False
assert bool([1]) is True
assert bool({}) is False
assert bool(None) is False

# str() basic
assert str(123) == '123'
assert str(3.14) == '3.14'
assert str(None) == 'None'
assert str(True) == 'True'
assert str([1, 2]) == '[1, 2]'

# int() basic
assert int(3.14) == 3
assert int("42") == 42
assert int("1010", 2) == 10
assert int("ff", 16) == 255

# float() basic
assert float(3) == 3.0
assert float("3.14") == 3.14

# hex, oct, bin
assert hex(255) == '0xff'
assert oct(8) == '0o10'
assert bin(5) == '0b101'

print("test_builtin_extended passed")
