"""
Regression: round(float, ndigits) must match CPython's decimal-based
rounding for ".xx5"-style values.

CPython 3.14 reference:
    round(2.675, 2) == 2.67   # double is 2.674999..., CPython rounds to 2.67
    round(1.005, 2) == 1.0    # double is 1.0049999..., CPython rounds to 1.0
    round(2.5) / round(3.5) / round(0.5) use banker's rounding (half-to-even)
"""

# The bug: Math.Round(2.675, 2) == 2.68, but CPython says 2.67
assert round(2.675, 2) == 2.67
assert round(1.005, 2) == 1.0

# half-to-even (banker's rounding) must not regress
assert round(2.5) == 2
assert round(3.5) == 4
assert round(0.5) == 0

# Existing decimal-rounding cases still hold
assert round(3.14159, 2) == 3.14
assert round(1234.5678, -2) == 1200.0

print("test_round_regression passed")
