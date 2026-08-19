"""
Standard list operations and method tests
"""

# Basic list initialization and indexing
a = [1, 2, 3]
assert a[0] == 1
assert a[1] == 2
assert a[2] == 3
assert a[-1] == 3
assert a[-2] == 2
assert a[-3] == 1

# Modification methods
a.append(4)
assert a == [1, 2, 3, 4]

a.extend([5, 6])
assert a == [1, 2, 3, 4, 5, 6]

a.insert(0, 0)
assert a[0] == 0
assert a == [0, 1, 2, 3, 4, 5, 6]

# Removing elements
a.remove(3)
assert a == [0, 1, 2, 4, 5, 6]

v = a.pop()
assert v == 6
assert a == [0, 1, 2, 4, 5]

v = a.pop(1)
assert v == 1
assert a == [0, 2, 4, 5]

# Content inspection
a = [1, 2, 2, 3, 4, 2]
assert a.count(2) == 3
assert a.count(5) == 0
assert a.index(2) == 1

# Reordering methods
a = [3, 1, 4, 1, 5, 9, 2, 6, 5]
a.sort()
assert a == [1, 1, 2, 3, 4, 5, 5, 6, 9]

a.reverse()
assert a == [9, 6, 5, 5, 4, 3, 2, 1, 1]

# Copying
b = a.copy()
assert b == a
assert b is not a

# Clearing
a.clear()
assert len(a) == 0
assert a == []

# Containment
a = [10, 20, 30]
assert 10 in a
assert 40 not in a
assert 20 in a

# Operators
a = [1, 2] + [3, 4]
assert a == [1, 2, 3, 4]

a += [5, 6]
assert a == [1, 2, 3, 4, 5, 6]

a = [0] * 3
assert a == [0, 0, 0]

a *= 2
assert a == [0, 0, 0, 0, 0, 0]

# Slicing
a = [0, 1, 2, 3, 4, 5]
assert a[1:4] == [1, 2, 3]
assert a[:3] == [0, 1, 2]
assert a[3:] == [3, 4, 5]
assert a[:] == [0, 1, 2, 3, 4, 5]
assert a[::2] == [0, 2, 4]
assert a[::-1] == [5, 4, 3, 2, 1, 0]

a[1:3] = [10, 20, 30]
assert a == [0, 10, 20, 30, 3, 4, 5]

del a[1:4]
assert a == [0, 3, 4, 5]

# Comparison
assert [1, 2, 3] == [1, 2, 3]
assert [1, 2] < [1, 2, 3]
assert [1, 3] > [1, 2, 4]
assert [1, 2] <= [1, 2]
assert [1, 2] >= [1, 2]

class A:
    def __gt__(self, other):
        return 1

assert ([A()] > [A()]) == 1

print("test_list passed")
