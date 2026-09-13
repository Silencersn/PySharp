"""
Regression: abs() of a complex value must dispatch the complex __abs__
slot and return hypot(real, imag) as a float (CPython complex_abs) —
it used to raise TypeError("bad operand type for abs(): 'complex'").
hypot overflow for finite components raises OverflowError.

CPython 3.14.6 reference:
    abs(3j)                        -> 3.0
    abs(complex(4, 3))             -> 5.0
    type(abs(3j))                  -> float
    abs(complex(2.5, 1.5))         -> 2.9154759474226504
    abs(complex(inf, 3))           -> inf
    abs(complex(nan, 0))           -> nan
    abs(complex(1.7e308, 1.7e308)) -> OverflowError: absolute value too large
    sorted([complex(3, 4), 0j, complex(0, 1)], key=abs)
                                   -> [0j, 1j, (3+4j)]
"""

assert abs(3j) == 3.0
assert abs(complex(4, 3)) == 5.0
assert abs(complex(0, -1)) == 1.0
assert abs(complex(-3, -4)) == 5.0
assert abs(complex(0, 0)) == 0.0
assert type(abs(3j)).__name__ == 'float'
assert abs(complex(2.5, 1.5)) == 2.9154759474226504

# non-finite components propagate instead of raising
assert str(abs(complex(float('inf'), 3))) == 'inf'
assert str(abs(complex(float('nan'), 0))) == 'nan'
assert str(abs(complex(0, float('nan')))) == 'nan'

# hypot overflow for finite components
try:
    abs(complex(1.7e308, 1.7e308))
except OverflowError as e:
    assert 'absolute value too large' in str(e)
else:
    raise AssertionError('expected OverflowError')

# abs() works as a sort key
assert str(sorted([complex(3, 4), 0j, complex(0, 1)], key=abs)) == '[0j, 1j, (3+4j)]'

print("test_complex_abs_regression passed")
