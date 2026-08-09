"""
Regression: pow(base, -exp, mod) must compute the modular inverse and return
an int (CPython 3.8+), instead of falling back to float modulo.

CPython 3.14 reference:
    pow(2, -1, 5)  == 3     # 2 * 3 == 1 mod 5
    pow(3, -2, 5)  == 4     # 3**-2 == 4 mod 5
    pow(2, -3, 5)  == 2     # 2**-3 == 2 mod 5
    pow(10, -1, 17) == 12   # 10 * 12 == 1 mod 17
    pow(2, -1, 4)  -> ValueError (2 and 4 are not coprime)
"""

assert pow(2, -1, 5) == 3
assert pow(3, -2, 5) == 4
assert pow(2, -3, 5) == 2
assert pow(10, -1, 17) == 12

# Base not invertible for the given modulus must raise ValueError
# (currently it silently returns a float).
try:
    pow(2, -1, 4)
    assert False, 'pow(2, -1, 4) should raise ValueError'
except ValueError:
    pass

print("test_pow_neg_mod_regression passed")
