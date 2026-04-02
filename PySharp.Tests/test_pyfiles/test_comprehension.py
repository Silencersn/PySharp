"""
List, Set, Dict comprehension and Generator expression tests
"""

# List comprehension
lst = [x * x for x in range(5)]
assert lst == [0, 1, 4, 9, 16]

# Set comprehension
s = {x % 3 for x in range(7)}
assert 0 in s and 1 in s and 2 in s

# Dict comprehension
d = {x: x * 2 for x in range(4)}
assert d[0] == 0 and d[1] == 2 and d[2] == 4 and d[3] == 6

# Nested comprehension
nested = [(i, j) for i in range(2) for j in range(2)]
assert nested == [(0, 0), (0, 1), (1, 0), (1, 1)]

# Conditional comprehension
filtered = [x for x in range(10) if x % 2 == 0]
assert filtered == [0, 2, 4, 6, 8]

# Scoping in comprehension
x = 100
scoped = [x for x in range(3)]
assert scoped == [0, 1, 2]
assert x == 100

# Generator expression
gen = (x * 2 for x in range(4))
assert list(gen) == [0, 2, 4, 6]

print("test_comprehension passed")
