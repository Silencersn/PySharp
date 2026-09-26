"""math.log, log2 and log10 on integer inputs convert through the double exactly like CPython's loghelper — exact powers of ten, the _PyLong_Frexp fallback for huge ints — with CPython's domain-error messages and two-arg log staying log(x)/log(base).

:kind: test
"""

# Regression test: math.log10/log2/log on integer inputs must go through
# the double conversion (CPython loghelper) instead of an inexact
# BigInteger logarithm — powers of ten return exact values, huge ints use
# the _PyLong_Frexp fallback, and domain errors carry CPython's messages.
import math

# exact powers of ten
for k in [-30, -22, -3, 0, 1, 3, 6, 10, 15, 18, 22, 23, 30, 100, 308]:
    v = 10 ** k if k >= 0 else float(10 ** k)
    assert math.log10(v) == float(k), (k, math.log10(v))
assert repr(math.log10(1000)) == '3.0'
assert repr(math.log10(10 ** 10)) == '10.0'
assert repr(math.log10(10 ** 22)) == '22.0'
assert repr(math.log10(10 ** 23)) == '23.0'

# huge ints take the _PyLong_Frexp fallback
assert repr(math.log10(10 ** 400)) == '400.0'
assert repr(math.log10(2 ** 2000)) == '602.0599913279624'
assert repr(math.log(2 ** 2000)) == '1386.2943611198907'
assert repr(math.log2(2 ** 2000)) == '2000.0'
assert repr(math.log10(2 ** 1024)) == '308.25471555991675'

# odd int inputs agree with the float path
for v in [3, 7, 12345, 10 ** 40 + 3]:
    assert repr(math.log2(v)) == repr(math.log2(float(v))), v
    assert repr(math.log10(v)) == repr(math.log10(float(v))), v

# non-positive ints raise the plain message
for v in [0, -1, -(10 ** 50)]:
    try:
        math.log10(v)
    except ValueError as e:
        assert str(e) == 'expected a positive input', (v, e)
    else:
        raise AssertionError(v)

# float path: message uses the repr of the original argument
for v, tail in [(0.0, '0.0'), (-0.0, '-0.0'), (-1.5, '-1.5')]:
    try:
        math.log10(v)
    except ValueError as e:
        assert str(e) == f'expected a positive input, got {tail}', (v, e)
    else:
        raise AssertionError(v)

# two-arg log stays log(x)/log(base)
assert repr(math.log(1000, 10)) == '2.9999999999999996'
assert repr(math.log(8, 2)) == '3.0'
assert repr(math.log(3, 7)) == repr(math.log(3.0) / math.log(7.0))

# log1p domain error carries the argument repr
try:
    math.log1p(-2.0)
except ValueError as e:
    assert str(e) == 'expected argument value > -1, got -2.0', e
else:
    raise AssertionError('log1p')

# overflow face: converting an index-only huge value to float raises
class BigIndex:
    def __index__(self):
        return 2 ** 2000
try:
    math.log10(BigIndex())
except OverflowError as e:
    assert str(e) == 'int too large to convert to float', e
else:
    raise AssertionError('BigIndex')

# negative index-only value gets the float-path message
class NegIndex:
    def __index__(self):
        return -3
try:
    math.log10(NegIndex())
except ValueError as e:
    assert 'expected a positive input' in str(e), e
else:
    raise AssertionError('NegIndex')

print("test_math_log_int_parity passed")
