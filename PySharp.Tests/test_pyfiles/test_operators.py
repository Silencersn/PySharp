"""
Operator tests (arithmetic, logical, bitwise, and rich comparison)
"""

import operator

# Arithmetic operators
assert (1 + 2) == 3
assert (5 - 3) == 2
assert (4 * 3) == 12
assert (8 / 2) == 4.0
assert (7 // 2) == 3
assert (7 % 3) == 1
assert (2 ** 3) == 8

# Division by zero
try:
    1 / 0
    assert False, "Should raise ZeroDivisionError"
except ZeroDivisionError:
    pass

try:
    1 // 0
    assert False, "Should raise ZeroDivisionError"
except ZeroDivisionError:
    pass

try:
    1 % 0
    assert False, "Should raise ZeroDivisionError"
except ZeroDivisionError:
    pass

# Mismatched types
try:
    1 + "a"
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    "a" * "b"
    assert False, "Should raise TypeError"
except TypeError:
    pass

# Bitwise operators
assert (8 << 1) == 16
assert (8 >> 1) == 4
assert (5 & 3) == 1
assert (5 | 2) == 7
assert (5 ^ 3) == 6

# Comparison operators
assert (3 < 4) is True
assert (3 <= 3) is True
assert (3 == 3) is True
assert (3 != 4) is True
assert (5 > 2) is True
assert (5 >= 5) is True

# Comparison with mismatched types
try:
    3 < "a"
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    None < 1
    assert False, "Should raise TypeError"
except TypeError:
    pass

# operator module tests
assert operator.add(1, 2) == 3
assert operator.sub(5, 3) == 2
assert operator.mul(4, 3) == 12
assert operator.truediv(8, 2) == 4.0
assert operator.floordiv(7, 2) == 3
assert operator.mod(7, 3) == 1
assert operator.pow(2, 3) == 8
assert operator.lshift(8, 1) == 16
assert operator.rshift(8, 1) == 4
assert operator.and_(5, 3) == 1
assert operator.or_(5, 2) == 7
assert operator.xor(5, 3) == 6
assert operator.lt(3, 4) is True
assert operator.le(3, 3) is True
assert operator.eq(3, 3) is True
assert operator.ne(3, 4) is True
assert operator.gt(5, 2) is True
assert operator.ge(5, 5) is True

# Test custom operator implementations
class C:
    def __init__(self, v):
        self.v = v
    def __add__(self, other):
        return C(self.v + other.v)
    def __eq__(self, other):
        if not isinstance(other, C):
            return False
        return self.v == other.v

a = C(1)
b = C(2)
c = a + b
assert isinstance(c, C)
assert c == C(3)

# Test reflective operators (__radd__)
class A:
    def __add__(self, other):
        return f"A+{other}"
    def __radd__(self, other):
        return f"{other}+A"
    def __eq__(self, other):
        return isinstance(other, A)

a = A()
assert (a + 1) == 'A+1'
assert (1 + a) == '1+A'

print("test_operators passed")
