# Regression: a zero-length format spec must make __format__ equivalent
# to str(obj) (CPython _PyLong_FormatAdvancedWriter / _PyFloat_FormatAdvancedWriter).
# bool inherited int's Format slot and rendered '1'/'0' in f-string
# interpolation and format(); float rendered the 'g' default-precision
# form instead of the shortest-repr str(float).

# int / bool (issue-217 matrix)
assert format(True) == 'True'
assert format(False) == 'False'
assert f"{True}" == 'True'
assert f"v={2 > 1}" == 'v=True'
assert f"v={not True}" == 'v=False'
assert str(True) == 'True'
assert "{}".format(True) == 'True'
assert "%s" % True == 'True'
assert f"{True!r}" == 'True'
assert f"{True=}" == 'True=True'

# non-empty specs keep the int delegation (bool formats as its value)
assert format(True, '3') == '  1'
assert format(True, '05') == '00001'
assert format(True, 'x') == '1'
assert f"{True:5.2f}" == ' 1.00'
assert "{0:3}".format(True) == '  1'

# guards: exact ints unchanged
assert format(1) == '1'
assert format(-7) == '-7'
assert f"{1}" == '1'
assert format(1000000, ',') == '1,000,000'

# float: empty spec == str(float) == shortest repr
assert format(1.5) == '1.5'
assert format(123.456) == '123.456'
assert format(1e15) == '1000000000000000.0'
assert format(1e16) == '1e+16'
assert format(9.223372036854776e+18) == '9.223372036854776e+18'
assert format(0.0001) == '0.0001'
assert format(1e-05) == '1e-05'
assert format(-0.0) == '-0.0'
assert format(float('inf')) == 'inf'
assert format(float('nan')) == 'nan'
assert f"{1e17}" == '1e+17'
assert f"{123.456}" == '123.456'

# float non-empty specs unchanged
assert format(1.5, '.3g') == '1.5'
assert f"{1.5:8.2f}" == '    1.50'

# issue-124 original repro: integral floats keep the .0 in f-string
# implicit conversion (no conversion and empty spec both route to str)
assert f'{1.0}' == '1.0'
assert f'{2.0}' == '2.0'
assert f'{-1.0}' == '-1.0'
assert f'{10.0}' == '10.0'
assert f'{1e2}' == '100.0'
assert f'{4.5}' == '4.5'
assert f'{-0.0}' == '-0.0'
assert str(1.0) == '1.0'
assert '{}'.format(1.0) == '1.0'
assert f'{1.0!s}' == '1.0'
x = 3.0
assert f'{x}' == '3.0'
assert f'{x!s}' == '3.0'
assert f'{x:}' == '3.0'

# issue-168 original repro: comparison/membership bools render as
# True/False, including through a variable
assert f'{1 < 2}' == 'True'
assert f'{2 > 1}' == 'True'
assert f'{1 in [1]}' == 'True'
assert f'{1 < 2 < 3}' == 'True'
y = 1 < 2
assert f'{y}' == 'True'
assert f'{y!s}' == 'True'
assert f'{1 < 2!s}' == 'True'

print("test_format_empty_spec_regression passed")
