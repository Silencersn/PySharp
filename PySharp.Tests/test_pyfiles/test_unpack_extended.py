"""
Tests for * and ** unpacking syntax - lists, tuples, dicts, function calls
"""
# * in list
a = [1, 2, 3]
b = [*a, 4, 5]
assert b == [1, 2, 3, 4, 5]
c = [0, *a]
assert c == [0, 1, 2, 3]
d = [*a, *a]
assert d == [1, 2, 3, 1, 2, 3]
e = [*[10, 20], 30, *[40]]
assert e == [10, 20, 30, 40]

# * in tuple
g = (*a, 4)
assert g == (1, 2, 3, 4)
h = (0, *a)
assert h == (0, 1, 2, 3)

# ** in dict
d1 = {"x": 1, "y": 2}
d2 = {**d1, "z": 3}
assert d2 == {"x": 1, "y": 2, "z": 3}
d3 = {"x": 99, **d1}
assert d3["x"] == 1
d4 = {**{"a": 1}, **{"b": 2}}
assert d4 == {"a": 1, "b": 2}

# * in function calls
def sum3(a, b, c):
    return a + b + c
assert sum3(*[10, 20, 30]) == 60
assert sum3(*(1, 2, 3)) == 6

# ** in function calls
def pt(x, y):
    return (x, y)
assert pt(**{"x": 5, "y": 10}) == (5, 10)
def fdef(a, b=0, c=0):
    return a + b + c
assert fdef(**{"a": 1, "c": 3}) == 4

# mixed * and **
def mix(a, b, c, d):
    return a + b + c + d
assert mix(*[1, 2], c=3, **{"d": 4}) == 10

# *args with * unpack
def vargs(*args):
    return sum(args)
assert vargs(*[1, 2, 3]) == 6
assert vargs(*(4, 5)) == 9

print("test_unpack_extended passed")
