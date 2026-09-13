"""
Regression: 0 (or 0.0, including -0.0) raised to a negative power must raise
ZeroDivisionError('zero to a negative power'), not return inf. Covers the
int ** int, int ** float (reflected), and float ** float paths plus the
in-place **= form.

CPython 3.14 reference:
    0 ** -1        -> ZeroDivisionError: zero to a negative power
    0.0 ** -1      -> ZeroDivisionError: zero to a negative power
    0 ** 0         -> 1
    0.0 ** -0.0    -> 1.0
    (-2) ** -1     -> -0.5
    pow(0, -1, 5)  -> ValueError: base is not invertible for the given modulus

Previously every zero-base negative-exponent combination returned inf because
the .NET pow helpers have no zero-base guard.
"""


def zero_division_message(fn):
    try:
        fn()
    except ZeroDivisionError as e:
        return str(e)
    return None


# zero base, negative exponent -> ZeroDivisionError on every type path
assert zero_division_message(lambda: 0 ** -1) == 'zero to a negative power'
assert zero_division_message(lambda: 0 ** -2) == 'zero to a negative power'
assert zero_division_message(lambda: 0 ** -0.5) == 'zero to a negative power'
assert zero_division_message(lambda: 0.0 ** -1) == 'zero to a negative power'
assert zero_division_message(lambda: 0.0 ** -1.5) == 'zero to a negative power'
assert zero_division_message(lambda: -0.0 ** -1) == 'zero to a negative power'
assert zero_division_message(lambda: (-0.0) ** -2) == 'zero to a negative power'
assert zero_division_message(lambda: False ** -1) == 'zero to a negative power'
assert zero_division_message(lambda: pow(0, -1)) == 'zero to a negative power'
assert zero_division_message(lambda: pow(0.0, -1)) == 'zero to a negative power'

# in-place form follows the same rule
x = 0
try:
    x **= -1
except ZeroDivisionError:
    pass
else:
    raise AssertionError('0 **= -1 must raise ZeroDivisionError')

# non-negative exponents and non-zero bases keep working
assert 0 ** 0 == 1
assert 0 ** 1 == 0
assert 0.0 ** 0 == 1.0
assert 0.0 ** -0.0 == 1.0
assert 2 ** -1 == 0.5
assert (-2) ** -1 == -0.5
assert (-2.0) ** -1 == -0.5
assert 0.5 ** -1 == 2.0

# ternary pow with a zero base and negative exponent keeps the inverse rule
try:
    pow(0, -1, 5)
except ValueError as e:
    assert str(e) == 'base is not invertible for the given modulus'
else:
    raise AssertionError('pow(0, -1, 5) must raise ValueError')

print("test_pow_zero_negative_exponent_regression passed")
