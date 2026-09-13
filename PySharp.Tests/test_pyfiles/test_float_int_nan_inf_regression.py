"""
Regression: int() on NaN/infinity floats must raise catchable ValueError /
OverflowError instead of crashing with a .NET OverflowException, and
round(x) without ndigits must raise the same instead of a bare TypeError.
With ndigits given, NaN/infinities round to themselves.

CPython 3.14 reference:
    int(float('nan'))  -> ValueError: cannot convert float NaN to integer
    int(float('inf'))  -> OverflowError: cannot convert float infinity to integer
    round(float('nan')) -> ValueError
    round(float('inf')) -> OverflowError
    round(float('nan'), 1) -> nan
"""

inf = float('inf')

# int(): catchable errors, not a hard crash
try:
    int(float('nan'))
except ValueError as e:
    assert str(e) == 'cannot convert float NaN to integer'
else:
    raise AssertionError('int(nan) must raise ValueError')

try:
    int(inf)
except OverflowError as e:
    assert str(e) == 'cannot convert float infinity to integer'
else:
    raise AssertionError('int(inf) must raise OverflowError')

try:
    int(-inf)
except OverflowError as e:
    assert str(e) == 'cannot convert float infinity to integer'
else:
    raise AssertionError('int(-inf) must raise OverflowError')

# round(): same errors for the no-ndigits form
try:
    round(float('nan'))
except ValueError as e:
    assert str(e) == 'cannot convert float NaN to integer'
else:
    raise AssertionError('round(nan) must raise ValueError')

try:
    round(inf)
except OverflowError as e:
    assert str(e) == 'cannot convert float infinity to integer'
else:
    raise AssertionError('round(inf) must raise OverflowError')

try:
    round(-inf)
except OverflowError as e:
    assert str(e) == 'cannot convert float infinity to integer'
else:
    raise AssertionError('round(-inf) must raise OverflowError')

# with ndigits the non-finite values round to themselves
assert str(round(float('nan'), 1)) == 'nan'
assert round(inf, 1) == inf
assert round(-inf, 1) == -inf

# ordinary conversions keep working
assert int(3.7) == 3
assert int(-3.7) == -3
assert round(2.5) == 2
assert round(3.5) == 4
assert int(1e300) == int(1e300)

print("test_float_int_nan_inf_regression passed")
