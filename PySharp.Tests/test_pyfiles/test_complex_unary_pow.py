"""Complex unary minus negates both components and unary plus returns an exact complex unchanged, and complex ** supports integer, fractional (polar principal branch) and zero exponents, raising ZeroDivisionError for zero to a negative power.

:kind: test
"""

# Regression: complex supports the unary -/+ slots (CPython
# complex_neg negates both components, complex_pos returns an exact
# instance unchanged) and the ** slot for complex-left operands
# (integer exponents, fractional exponents via the polar principal
# branch, and the zero-exponent identity).
inf = float("inf")
nan = float("nan")
z = complex(3, 4)

assert -z == complex(-3, -4)
assert +z == complex(3, 4)
assert -(-z) == z
assert repr(-complex(0, 0)) == "(-0-0j)"
assert repr(+complex(0, 0)) == "0j"
assert repr(-complex(-0.0, -0.0)) == "0j"
assert repr(-complex(inf, 0)) == "(-inf-0j)"
assert repr(-complex(nan, 0)) == "(nan-0j)"
assert type(+z).__name__ == "complex"
assert -z + z == 0j

assert z ** 2 == complex(-7, 24)
assert complex(2, 0) ** 0.5 == complex(1.4142135623730951, 0)
assert repr(complex(1, 1) ** complex(1, 1)) == repr(complex(0.2739572538301211, 0.5837007587586147))
assert z ** 0 == complex(1, 0)
assert repr(z ** -2) == repr(complex(-0.0112, -0.0384))
assert pow(z, 3) == complex(-117, 44)
assert complex(0, 0) ** 2 == 0j
try:
    complex(0, 0) ** -1
    raise AssertionError("expected ZeroDivisionError")
except ZeroDivisionError as e:
    assert str(e) == "zero to a negative or complex power"

assert abs(z) == 5.0
assert complex(3, 4) + 1 == complex(4, 4)
assert complex(1, 1) == complex(1, 1)

print("test_complex_unary_pow passed")
