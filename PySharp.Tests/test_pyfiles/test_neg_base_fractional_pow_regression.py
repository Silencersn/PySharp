# Regression: float_pow's negative-base rule — a finite negative base
# with a non-integral exponent returns the complex principal value
# (float_pow returns NotImplemented and complex_pow's hypot/atan2 form
# evaluates it), never a silent float nan. The integral-exponent,
# zero, infinity and NaN special cases keep their float results, and
# the exponent itself must arrive exactly, so int/int true division
# follows long_true_divide's round-half-to-even quotient conversion.

def is_complex_principal(value, expect):
    assert type(value).__name__ == "complex", type(value)
    assert value == expect, (value, expect)

# the issue's matrix, verified value-for-value against CPython 3.14
is_complex_principal((-4) ** 0.5, 1.2246467991473532e-16 + 2j)
is_complex_principal((-8) ** (1 / 3), 1.0000000000000002 + 1.7320508075688772j)
is_complex_principal((-2.0) ** 0.5, 8.659560562354934e-17 + 1.4142135623730951j)
is_complex_principal((-2.0) ** (1 / 3), 0.6299605249474367 + 1.0911236359717214j)
is_complex_principal((-0.5) ** 0.5, 4.329780281177467e-17 + 0.7071067811865476j)
is_complex_principal((-2.0) ** -0.5, 4.329780281177467e-17 - 0.7071067811865476j)
is_complex_principal((-4) ** 1.5, -1.4695761589768238e-15 - 8j)
is_complex_principal((-1) ** 0.5, 6.123233995736766e-17 + 1j)
is_complex_principal((-100.0) ** 0.25, 2.23606797749979 + 2.23606797749979j)
is_complex_principal(pow(-4, 0.5), 1.2246467991473532e-16 + 2j)

# integral exponents keep float semantics (sign applied, no complex)
assert (-2.0) ** 2 == 4.0 and (-2.0) ** 3 == -8.0
assert (-2.0) ** -1 == -0.5 and (-2) ** 2 == 4
assert (-4) ** 0.0 == 1.0
assert repr((-0.0) ** 0.5) == "0.0"

# zero to a negative power raises for both operand types
for src in ["0 ** -1", "0.0 ** -1"]:
    try:
        exec(src, globals())
        raise AssertionError("expected ZeroDivisionError for " + src)
    except ZeroDivisionError as e:
        assert str(e) == "zero to a negative power", (src, str(e))

# infinity and NaN special cases stay float
inf = float("inf")
nan = float("nan")
assert float("-inf") ** 0.5 == inf
assert (-4) ** inf == inf
assert (-4) ** nan != (-4) ** nan
assert nan ** 0.5 != nan ** 0.5

# the exponent must be exact: int/int true division rounds the
# quotient half-to-even like CPython, instead of truncating
assert repr(1 / 3) == "0.3333333333333333"
assert repr(10 / 3) == "3.3333333333333335"
assert repr(10**400 / 10**100) == "1e+300"
assert repr(2**70 / 3) == "3.935305402391371e+20"
assert repr(-(2**54 - 1) / 7) == "-2573485501354569.0"

print("test_neg_base_fractional_pow_regression passed")
