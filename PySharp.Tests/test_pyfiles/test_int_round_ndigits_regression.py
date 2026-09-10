"""
Regression: round(int, ndigits) with a negative ndigits must round to the
nearest multiple of 10 ** -ndigits using round-half-to-even (CPython
long_round's divmod_near), not return the value unchanged. Ties go to the
even multiple in both directions (1250 -> 1200, 1350 -> 1400,
-1250 -> -1200, -1350 -> -1400); non-negative ndigits stays the identity
and bool/int-subclass instances still convert to exact ints.
"""

# negative ndigits: decimal power-of-ten rounding
assert round(1234, -2) == 1200
assert round(-1234, -2) == -1200
assert round(1500, -3) == 2000
assert round(999, -3) == 1000
assert round(1000, -3) == 1000
assert round(0, -2) == 0
assert round(123456789, -6) == 123000000
assert round(-123456789, -6) == -123000000
assert round(2 ** 70, -10) == 1180591620720000000000

# half-to-even ties, both signs
assert round(1250, -2) == 1200
assert round(1350, -2) == 1400
assert round(1150, -2) == 1200
assert round(-1250, -2) == -1200
assert round(-1350, -2) == -1400
assert round(2500, -3) == 2000
assert round(3500, -3) == 4000
assert round(-2500, -3) == -2000

# non-negative ndigits: identity, exact ints return themselves
assert round(1234, 2) == 1234
assert round(1234567, 4) == 1234567
assert round(5, 10 ** 20) == 5

# bool and int subclasses convert to exact ints
assert round(True, -2) == 0
assert round(False, -1) == 0


class I(int):
    pass

assert round(I(7)) == 7
assert round(I(1500), -3) == 2000

# ndigits goes through __index__; failure messages match CPython
class Idx:
    def __index__(self):
        return -2

assert round(1250, Idx()) == 1200
assert round(5, True) == 5

try:
    round(5, 'x')
except TypeError as e:
    assert str(e) == "'str' object cannot be interpreted as an integer", str(e)
else:
    raise AssertionError("expected TypeError")

try:
    round('5')
except TypeError as e:
    assert str(e) == "type str doesn't define __round__ method", str(e)
else:
    raise AssertionError("expected TypeError")

print("test_int_round_ndigits_regression passed")
