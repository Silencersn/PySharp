a = [1, 2, 3]
assert a[0] == 1
assert a[-1] == 3
a.append(4)
assert a == [1, 2, 3, 4]
a.extend([5, 6])
assert a == [1, 2, 3, 4, 5, 6]
a.insert(0, 0)
assert a[0] == 0
a.remove(3)
assert a == [0, 1, 2, 4, 5, 6]
v = a.pop()
assert v == 6
assert a == [0, 1, 2, 4, 5]
a.clear()
assert a == []
a = [1, 2, 2, 3]
assert a.count(2) == 2
assert a.index(2) == 1
a.sort(reverse=True)
assert a == [3, 2, 2, 1]
a.reverse()
assert a == [1, 2, 2, 3]
b = a.copy()
assert b == a and b is not a
assert 2 in a
assert 5 not in a
