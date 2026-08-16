"""
Regression: old-style %e/%E/%g/%G float formatting must match CPython.

CPython 3.14 reference:
    '%e' % 1.0          == '1.000000e+00'   (exponent 2 digits, not 3)
    '%e' % 12345.6789   == '1.234568e+04'
    '%e' % 1e100        == '1.000000e+100'  (3 digits only when >= 100)
    '%g' % 1e6          == '1e+06'          (lowercase e, not '1E+06')
    '%G' % 1e6          == '1E+06'
    '%#g' % 1.0         == '1.00000'        (trailing zeros kept)
    '%#g' % 0.0         == '0.00000'
    '%.0g' % 12345.0    == '1e+04'          (precision 0 -> 1 sig digit)
"""

# --- %e / %E: exponent must use at least 2 digits (not fixed 3) ---
assert '%e' % 1.0 == '1.000000e+00'
assert '%e' % 12345.6789 == '1.234568e+04'
assert '%E' % 1.0 == '1.000000E+00'
assert '%.3e' % 1.0 == '1.000e+00'
assert '%e' % 123456789.0 == '1.234568e+08'
assert '%e' % 123456.0 == '1.234560e+05'
assert '%e' % -12345.6789 == '-1.234568e+04'
assert '%e' % 1e-5 == '1.000000e-05'
# 3 digits only when the exponent is >= 100
assert '%e' % 1e100 == '1.000000e+100'
assert '%e' % 1e-100 == '1.000000e-100'
# precision 0 -> no fractional digits
assert '%.0e' % 1.0 == '1e+00'
assert '%#.0e' % 1.0 == '1.e+00'
assert '%#.0e' % 12345.0 == '1.e+04'
# sign / space flags
assert '%+e' % 1.0 == '+1.000000e+00'
assert '% e' % 1.0 == ' 1.000000e+00'

# --- %g / %G: lowercase e for %g, uppercase for %G ---
assert '%g' % 1e6 == '1e+06'
assert '%g' % 0.00001 == '1e-05'
assert '%g' % 1234567.0 == '1.23457e+06'
assert '%.2g' % 123456.0 == '1.2e+05'
assert '%g' % -1e6 == '-1e+06'
assert '%G' % 1e6 == '1E+06'
assert '%g' % 1.0 == '1'
assert '%g' % 0.0001 == '0.0001'
assert '%g' % 999999.0 == '999999'
assert '%g' % 1000000.0 == '1e+06'
assert '%g' % 12345.0 == '12345'
assert '%.17g' % 0.1 == '0.10000000000000001'
assert '%g' % 0.0 == '0'
assert '%g' % -0.0 == '-0'
# low precision rounding (round-half-even like CPython)
assert '%.0g' % 12345.0 == '1e+04'
assert '%.1g' % 2.5 == '2'
assert '%.1g' % 15.0 == '2e+01'
assert '%.2g' % 12500.0 == '1.2e+04'
assert '%.3g' % 9995.0 == '1e+04'

# --- %#g: keep trailing zeros and force a decimal point ---
assert '%#g' % 1.0 == '1.00000'
assert '%#g' % 1e6 == '1.00000e+06'
assert '%#g' % 123456.0 == '123456.'
assert '%#g' % 1234567.0 == '1.23457e+06'
assert '%#g' % 0.0001 == '0.000100000'
assert '%#g' % 0.00001 == '1.00000e-05'
assert '%#g' % 0.0 == '0.00000'
assert '%#g' % 999999.0 == '999999.'
assert '%#g' % 12345.0 == '12345.0'
assert '%#.5g' % 1.0 == '1.0000'
assert '%#.0g' % 1.0 == '1.'
assert '%#.0g' % 1e6 == '1.e+06'
assert '%#.0g' % 0.0 == '0.'
assert '%#.3g' % 12345.0 == '1.23e+04'
assert '%#G' % 1e6 == '1.00000E+06'
assert '%#G' % 1.0 == '1.00000'

# --- zero-padding with sign for floats ---
assert '%+05g' % 1.0 == '+0001'
assert '%#010.3g' % 1.0 == '0000001.00'
assert '%#010.3g' % -1.0 == '-000001.00'

# --- nan / inf: %e/%g lowercase, %E/%G uppercase, sign flags apply ---
assert '%e' % float('inf') == 'inf'
assert '%e' % float('-inf') == '-inf'
assert '%e' % float('nan') == 'nan'
assert '%E' % float('inf') == 'INF'
assert '%E' % float('nan') == 'NAN'
assert '%g' % float('-inf') == '-inf'
assert '%G' % float('inf') == 'INF'
assert '%+e' % float('inf') == '+inf'
assert '%+e' % float('-inf') == '-inf'
assert '% e' % float('inf') == ' inf'
assert '%+g' % float('nan') == '+nan'
assert '%f' % float('inf') == 'inf'
assert '%F' % float('inf') == 'INF'
# -0.0 counts as negative, so no '+' flag is added
assert '%+f' % -0.0 == '-0.000000'
assert '%+g' % -0.0 == '-0'

print("test_percent_format_float_regression passed")
