"""
Regression: complex str/repr must follow CPython complex_repr — a +0.0
real part drops the parentheses and the real part (1j, -1j, 0j), while a
-0.0 real part keeps the parenthesised form ((-0+0j)); the imaginary part
keeps its sign including -0.0 ((1-0j), -0j). The same rendering shows
through containers, str.format and f-strings with an empty format spec.

CPython 3.14.6 reference:
    repr(1j)                  -> '1j'
    repr(complex(0, -1))      -> '-1j'
    repr(complex(2, -3))      -> '(2-3j)'
    repr(complex(1, 0))       -> '(1+0j)'
    repr(complex(-0.0, 0.0))  -> '(-0+0j)'
    repr(complex(1.0, -0.0))  -> '(1-0j)'
    repr(complex(0.0, -0.0))  -> '-0j'
    repr(complex(0, 0))       -> '0j'
    repr(complex(0, inf))     -> 'infj'
    repr(complex(nan, 1))     -> '(nan+1j)'
    format(2j)                -> '2j'
    str([1j, complex(2, -3)]) -> '[1j, (2-3j)]'
"""

assert repr(1j) == '1j'
assert str(1j) == '1j'
assert repr(complex(0, 1)) == '1j'
assert repr(complex(0, -1)) == '-1j'
assert str(complex(0, -2)) == '-2j'
assert repr(complex(2, -3)) == '(2-3j)'
assert repr(complex(1, 0)) == '(1+0j)'
assert repr(complex(-0.0, 0.0)) == '(-0+0j)'
assert repr(complex(1.0, -0.0)) == '(1-0j)'
assert repr(complex(0.0, -0.0)) == '-0j'
assert repr(complex(-0.0, -1.0)) == '(-0-1j)'
assert repr(complex(0, 0)) == '0j'
assert str(complex(0, 0)) == '0j'
assert repr(complex(2.5, 1.5)) == '(2.5+1.5j)'
assert repr(complex(1e16, 2)) == '(1e+16+2j)'
assert repr(complex(0, 2.5e-08)) == '2.5e-08j'
assert repr(complex(0, 1e16)) == '1e+16j'
assert repr(complex(0, float('inf'))) == 'infj'
assert repr(complex(0, float('-inf'))) == '-infj'
assert repr(complex(0, float('nan'))) == 'nanj'
assert repr(complex(float('inf'), 0)) == '(inf+0j)'
assert repr(complex(float('inf'), float('inf'))) == '(inf+infj)'
assert repr(complex(float('nan'), 1)) == '(nan+1j)'

# containers render elements through the same repr
assert str([1j, complex(2, -3)]) == '[1j, (2-3j)]'
assert str({1j: 'a'}) == "{1j: 'a'}"

# empty format spec follows str
assert format(2j) == '2j'
assert "{}".format(2j) == '2j'
assert "{}".format(complex(0, -2)) == '-2j'
assert f"{1j}" == '1j'
assert "{!r}".format(1j) == '1j'
assert format(complex(0.0, -0.0)) == '-0j'

print("test_complex_repr_pure_imaginary_regression passed")
