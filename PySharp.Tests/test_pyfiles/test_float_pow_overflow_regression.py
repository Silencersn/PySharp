"""
Regression: float `**` (and pow()) overflowing the double range raises
OverflowError with the errno args tuple, like CPython float_pow's
ERANGE handling — instead of silently returning +/-inf. Underflow to
zero/subnormal stays silent, and results with non-finite operands
(inf/nan) are not errors. The math.pow path already raised.

CPython 3.14 reference (floatobject.c float_pow): the platform pow runs
only with finite operands, so an infinite result means ERANGE, reported
through PyErr_SetFromErrno -> args (34, 'Result too large') on Windows.
"""

inf = float('inf')
nan = float('nan')

def e(fn):
    try:
        return ('ok', fn())
    except Exception as ex:
        return (type(ex).__name__, str(ex))

expected_overflow = ('OverflowError', "(34, 'Result too large')")

# overflow raises, for the operator, pow(), and int exponent/base mixes
assert e(lambda: 2.0 ** 1024) == expected_overflow
assert e(lambda: (-2.0) ** 1025) == expected_overflow
assert e(lambda: 10.0 ** 400) == expected_overflow
assert e(lambda: pow(2.0, 1024)) == expected_overflow
assert e(lambda: 2 ** 1024.0) == expected_overflow
assert e(lambda: 2 ** 1024.0 is None) == expected_overflow

# in-range values unchanged
assert e(lambda: 2.0 ** 1023) == ('ok', 8.98846567431158e+307)
assert e(lambda: 1.5 ** 1024) == ('ok', 2.0770611040332967e+180)
assert e(lambda: 2.0 ** 0.5) == ('ok', 1.4142135623730951)

# addition does not overflow-check (comparison face)
assert e(lambda: 1e308 + 1e308) == ('ok', inf)

# underflow is silent
assert e(lambda: 2.0 ** -1075) == ('ok', 5e-324)
assert e(lambda: 2.0 ** -1080) == ('ok', 0.0)

# non-finite operands are not errors
assert e(lambda: float('inf') ** 2) == ('ok', inf)
assert repr(float('nan') ** 2) == 'nan'
assert repr(2.0 ** float('nan')) == 'nan'
assert e(lambda: float('inf') ** -1) == ('ok', 0.0)
assert e(lambda: (-float('inf')) ** 3) == ('ok', -inf)
assert e(lambda: 1e200 ** float('inf')) == ('ok', inf)

# zero-base negative power stays ZeroDivisionError
assert e(lambda: 0.0 ** -1) == ('ZeroDivisionError', 'zero to a negative power')
assert e(lambda: (-0.0) ** -2) == ('ZeroDivisionError', 'zero to a negative power')
assert e(lambda: 0.0 ** -0.0) == ('ok', 1.0)

# math.pow parity
import math
try:
    math.pow(2, 1024)
    assert False, "OverflowError expected"
except OverflowError:
    pass

print("test_float_pow_overflow_regression passed")
