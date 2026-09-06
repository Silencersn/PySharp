# Regression: float repr/str must render CPython's shortest round-trip
# digits with format_float_short presentation - lowercase 'e', exponent
# at least two digits, and scientific notation switched on exactly at the
# CPython thresholds (decimal point <= -4 or > 16: repr(1e16) is '1e+16',
# repr(1e15) stays fixed). PySharp used .NET "G" output verbatim, which
# emits uppercase 'E' and keeps 1e16 in fixed form.

# issue table (uppercase E)
assert repr(1e17) == '1e+17'
assert repr(-1e-100) == '-1e-100'
assert repr(1.5e300) == '1.5e+300'
assert repr(1.25e-8) == '1.25e-08'
assert repr(1e22) == '1e+22'
assert repr(9.223372036854776e+18) == '9.223372036854776e+18'
assert repr(1e30) == '1e+30'
assert repr(1.5e20) == '1.5e+20'
assert repr(-2.5e25) == '-2.5e+25'
assert repr(1e-30) == '1e-30'
assert repr(2**100 + 0.5) == '1.2676506002282294e+30'
assert str(1e17) == '1e+17'
assert str(1.25e-8) == '1.25e-08'

# threshold: >= 1e16 goes scientific, 1e15 stays fixed
assert repr(1e16) == '1e+16'
assert repr(1e15) == '1000000000000000.0'
assert repr(9999999999999998.0) == '9999999999999998.0'
assert repr(1.5e16) == '1.5e+16'

# negative threshold: < 1e-4 goes scientific, 1e-4 stays fixed
assert repr(1e-05) == '1e-05'
assert repr(3.14e-05) == '3.14e-05'
assert repr(0.0001) == '0.0001'

# common values keep their exact shapes
assert repr(123.456) == '123.456'
assert repr(-123.456) == '-123.456'
assert repr(0.1) == '0.1'
# note: the literal pins the repr algorithm; int/int true division has a
# separate 1-ulp accuracy issue, so 1 / 3 must not be used here
assert repr(0.3333333333333333) == '0.3333333333333333'
assert repr(0.0) == '0.0'
assert repr(-0.0) == '-0.0'
assert repr(123.0) == '123.0'
assert repr(float('inf')) == 'inf'
assert repr(float('-inf')) == '-inf'
assert repr(float('nan')) == 'nan'

# str() shares the repr implementation
assert str(1e16) == '1e+16'
assert str(123.456) == '123.456'

# containers and %r embed float reprs
assert repr((1e17, 2.5e-8)) == '(1e+17, 2.5e-08)'
assert repr([1.5e300]) == '[1.5e+300]'
assert '%r' % 1e17 == '1e+17'

# float subclass inherits the repr slot


class F(float):
    pass


assert repr(F(1e17)) == '1e+17'

print("test_float_repr_scientific_regression passed")
