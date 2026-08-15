"""
Regression: pow() with a negative base and a modulus must return CPython's
normalized modulo result (in [0, |mod|)), not C# remainder semantics.

CPython 3.14 reference:
    pow(-2, 3, 5)   == 2
    pow(-2, 3, 7)   == 6
    pow(-3, 3, 5)   == 3
    pow(-2, 3, -5)  == -3
    pow(-2, 2, 5)   == 4
    pow(-4, 2, 5)   == 1
    pow(-2, -1, 5)  == 2
    pow(-2, -1, -5) == -3
    pow(-3, -2, 5)  == 4
    pow(-7, 3, 10)  == 7
"""

assert pow(-2, 3, 5) == 2
assert pow(-2, 3, 7) == 6
assert pow(-3, 3, 5) == 3
assert pow(-2, 3, -5) == -3
assert pow(-2, 2, 5) == 4
assert pow(-4, 2, 5) == 1
assert pow(-2, -1, 5) == 2
assert pow(-2, -1, -5) == -3
assert pow(-3, -2, 5) == 4
assert pow(-7, 3, 10) == 7

print("test_pow_neg_base_mod_regression passed")
