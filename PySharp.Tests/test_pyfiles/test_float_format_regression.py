"""
Regression: format()/f-string float 'g'/'G'/'n' (and 'e'/'E' exponent width)
must match CPython.

CPython 3.14 reference:
    format(1e16, 'g')    == '1e+16'      (lowercase e)
    format(0.0, 'g')     == '0'          (not '0.0')
    format(1234.5, 'n')  == '1234.5'     (n == g under C locale)
    format(1234.5, ',')  == '1,234.5'    (grouping only in fixed form)
    format(1234567.0, ',g') == '1.23457e+06'  (no grouping in exponent form)
    format(1234.5, '_n') raises ValueError "Cannot specify '_' with 'n'."
    format(1234.5, 'e')  == '1.234500e+03'   (exponent at least 2 digits)
"""

# --- 'g': lowercase e, zero -> '0' ---
assert format(1e16, 'g') == '1e+16'
assert format(1e-05, 'g') == '1e-05'
assert format(1e15, 'g') == '1e+15'
assert format(1e16, 'G') == '1E+16'
assert format(0.0, 'g') == '0'
assert format(0.0, 'G') == '0'
assert format(123.456, 'g') == '123.456'
assert format(1234.5, 'g') == '1234.5'
assert format(999999.0, 'g') == '999999'
assert format(1000000.0, 'g') == '1e+06'
assert format(0.0001, 'g') == '0.0001'
assert format(1.5, 'g') == '1.5'

# --- precision (0 -> 1 significant digit) ---
assert format(123.456, '.3g') == '123'
assert format(12345.0, '.0g') == '1e+04'
assert format(1.5, '.0g') == '2'
assert format(2.5, '.1g') == '2'
assert format(9995.0, '.3g') == '1e+04'
assert format(0.1, '.17g') == '0.10000000000000001'

# --- '#': alternate form keeps trailing zeros / forces decimal point ---
assert format(0.0, '#g') == '0.00000'
assert format(1.0, '#g') == '1.00000'
assert format(1e16, '#g') == '1.00000e+16'
assert format(1.0, '#.0g') == '1.'
assert format(1e6, '#.0g') == '1.e+06'

# --- 'n': equals 'g' under the C locale ---
assert format(1234.5, 'n') == '1234.5'
assert format(1234567.0, 'n') == '1.23457e+06'
assert format(1e16, 'n') == '1e+16'
assert format(0.0, 'n') == '0'

# --- grouping: only the integer part of fixed-point form ---
assert format(1234.5, ',') == '1,234.5'
assert format(1234.5, '_') == '1_234.5'
assert format(1234567.0, ',g') == '1.23457e+06'
assert format(999999.0, ',g') == '999,999'
assert format(100000.0, ',g') == '100,000'
assert format(100000.0, '_g') == '100_000'
assert format(0.0, ',g') == '0'
assert format(1e-5, ',g') == '1e-05'
assert format(1234.5, ',.6_') == '1,234.5'   # width grouping wins

# --- grouping is rejected with 'n' ---
try:
    format(1234.5, '_n')
    raise SystemExit("expected ValueError for '_n'")
except ValueError as e:
    assert "Cannot specify '_' with 'n'." in str(e)
try:
    format(1234.5, ',n')
    raise SystemExit("expected ValueError for ',n'")
except ValueError as e:
    assert "Cannot specify ',' with 'n'." in str(e)

# --- 'e' / 'E': exponent uses at least 2 digits (not .NET's fixed 3) ---
assert format(1234.5, 'e') == '1.234500e+03'
assert format(1234.5, 'E') == '1.234500E+03'
assert format(1.0, '.0e') == '1e+00'
assert format(0.0, 'e') == '0.000000e+00'
assert format(1e100, 'e') == '1.000000e+100'
assert format(1e-5, 'e') == '1.000000e-05'
assert format(1.0, '#.0e') == '1.e+00'
assert format(1234567.89, ',e') == '1.234568e+06'

# --- 'f' / '%' grouping (fixed form) ---
assert format(1234567.0, ',f') == '1,234,567.000000'
assert format(1234567.0, '_f') == '1_234_567.000000'
assert format(1234567.0, ',%') == '123,456,700.000000%'
assert format(1234567.0, '_%') == '123_456_700.000000%'

# --- f-string equivalents ---
assert f'{1e16:g}' == '1e+16'
assert f'{0.0:g}' == '0'
assert f'{1234.5:,}' == '1,234.5'
assert f'{1.0:#g}' == '1.00000'

# --- '0' (zero-pad) flag forces '0' fill even with an explicit align ---
assert format(-1.0, '=020.5g') == '-0000000000000000001'
assert format(-1.0, '<020.5g') == '-1000000000000000000'
assert format(1.0, '=020.5g') == '00000000000000000001'

print("test_float_format_regression passed")
