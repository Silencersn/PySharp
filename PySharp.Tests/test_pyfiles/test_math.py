"""
Tests for the math module
"""
import math

# Constants
assert abs(math.pi - 3.141592653589793) < 1e-15
assert abs(math.e - 2.718281828459045) < 1e-15
assert abs(math.tau - 6.283185307179586) < 1e-15

# Basic math functions
assert math.sqrt(4) == 2.0
assert math.sqrt(0) == 0.0
assert math.sqrt(2) > 1.4
assert math.sin(0) == 0.0
assert abs(math.sin(math.pi / 2) - 1.0) < 1e-15
assert math.cos(0) == 1.0
assert abs(math.cos(math.pi) - (-1.0)) < 1e-15

# Rounding functions
assert math.floor(3.7) == 3
assert math.floor(-3.7) == -4
assert math.ceil(3.2) == 4
assert math.ceil(-3.2) == -3
assert math.trunc(3.7) == 3
assert math.trunc(-3.7) == -3

# Absolute value
assert math.fabs(-3.5) == 3.5
assert math.fabs(0) == 0.0

# GCD and LCM
assert math.gcd(12, 8) == 4
assert math.gcd(0, 5) == 5
assert math.gcd(7, 13) == 1
assert math.lcm(12, 8) == 24
assert math.lcm(3, 5) == 15

# Logarithmic functions
assert math.log(100, 10) == 2.0
assert math.log(8, 2) == 3.0
assert math.log2(8) == 3.0
assert math.log10(100) == 2.0
assert math.log(math.e) == 1.0
assert math.log1p(0) == 0.0
assert abs(math.log1p(1) - math.log(2)) < 1e-15

# Exponential
assert math.exp(0) == 1.0
assert math.exp(1) > 2.718
assert math.pow(2, 3) == 8.0
assert math.pow(4, 0.5) == 2.0

# fmod and remainder
assert math.fmod(7, 3) == 1.0
assert math.remainder(7, 3) == 1.0

# copysign
assert math.copysign(-5, 2) == 5.0
assert math.copysign(5, -2) == -5.0

# atan2
assert abs(math.atan2(1, 1) - 0.7853981633974483) < 1e-15

# Error cases
try:
    math.sqrt(-1)
    assert False, "sqrt(-1) should raise ValueError"
except ValueError:
    pass

try:
    math.log(0)
    assert False, "log(0) should raise ValueError"
except ValueError:
    pass

try:
    math.acos(2)
    assert False, "acos(2) should raise ValueError"
except ValueError:
    pass

try:
    math.acos(-2)
    assert False, "acos(-2) should raise ValueError"
except ValueError:
    pass

try:
    math.asin(2)
    assert False, "asin(2) should raise ValueError"
except ValueError:
    pass

print("test_math passed")
