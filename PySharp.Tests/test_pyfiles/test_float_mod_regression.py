"""
Regression: float % must use Python modulo semantics (sign follows the
divisor), not C# remainder semantics (sign follows the dividend).

CPython 3.14 reference:
    -7.0 % 3   -> 2.0
     7.0 % -3  -> -2.0
     3 % -7.0  -> -4.0
    -7.0 % -3  -> -1.0
"""
import math

# Heterogeneous-sign float modulo (the bug: C# '%' is a remainder)
assert (-7.0 % 3) == 2.0
assert (7.0 % -3) == -2.0
assert (3 % -7.0) == -4.0
assert (-7.0 % -3) == -1.0

# int/float mixed operands
assert (-7 % 3.0) == 2.0
assert (7.0 % -3) == -2.0
assert (-3 % 7.0) == 4.0

# divmod must stay consistent with % and //
assert divmod(-7.0, 3) == (-3.0, 2.0)
assert divmod(7.0, -3) == (-3.0, -2.0)
assert divmod(-7.0, -3) == (2.0, -1.0)

# floor division must stay consistent with divmod
assert (-7.0 // 3) == -3.0
assert (7.0 // -3) == -3.0
assert (-7.0 // -3) == 2.0

# Zero remainder must keep the divisor's sign (4.0 % -2.0 == -0.0)
assert math.copysign(1.0, 4.0 % -2.0) == -1.0
assert math.copysign(1.0, -4.0 % 2.0) == 1.0

print("test_float_mod_regression passed")
