"""
Regression: abs() must clear the sign bit of a float (CPython float_abs /
fabs semantics), so abs(-0.0) is +0.0 and math.copysign sees a positive
result. int.__abs__ must convert exact-subtype instances (bool) to pooled
ints, and float subclasses round-trip through the same slot.
"""

import math

# float: negative zero loses its sign
assert abs(-0.0) == 0.0
assert str(abs(-0.0)) == '0.0'
assert math.copysign(1, abs(-0.0)) == 1.0
assert math.copysign(1, abs(-1.5)) == 1.0

# ordinary values unchanged
assert abs(0.0) == 0.0
assert abs(-1.5) == 1.5
assert abs(float('inf')) == float('inf')
assert abs(float('-inf')) == float('inf')
assert math.copysign(1, abs(float('-inf'))) == 1.0

# int / bool: bool converts to an exact int, never the bool singleton
assert abs(-0) == 0
assert abs(-5) == 5
assert abs(True) == 1
assert (abs(True) is True) is False
assert abs(True) is not True
assert abs(-2 ** 70) == 2 ** 70


# subclasses share the fixed slots
class F(float):
    pass

assert abs(F(-0.0)) == 0.0
assert math.copysign(1, abs(F(-0.0))) == 1.0
assert type(abs(F(-1.5))) is float


class I(int):
    pass

assert abs(I(-5)) == 5
assert abs(I(True)) == 1

print("test_abs_regression passed")
