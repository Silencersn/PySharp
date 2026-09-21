"""
Regression test: float() string parsing diverged from CPython on separators.
NumberStyles.AllowThousands let any ',' through with a silently wrong value
(the float grammar has no thousands separator), while digit-grouping
underscores were rejected outright.

CPython 3.14 semantics (verified):
  - float('1,000') / float('-1,5') / float('1,00,0') -> ValueError
  - underscores may sit between digits in any segment (integer, fraction,
    exponent), also after the Nd transform (floatobject.c:
    _Py_string_to_number_with_underscores validates and strips them)
  - misplaced underscores -> ValueError with the usual float message
"""

MSG = "could not convert string to float: "


def expect_ve(arg):
    try:
        float(arg)
    except ValueError as e:
        assert str(e) == MSG + repr(arg), str(e)
        return
    raise AssertionError("expected ValueError: " + arg)


# ===== 1. thousands separators are rejected, not silently rewritten =====

expect_ve('1,000')
expect_ve('1,000.5e1')
expect_ve('-1,5')
expect_ve('1,00,0')
expect_ve('1_0,5')          # underscore stripped first, comma still fails
expect_ve('1,5e1_0')

# ===== 2. underscores between digits are accepted in every segment =====

assert float('1_000') == 1000.0
assert float('1_0.5') == 10.5
assert float(' 1_0.5 ') == 10.5
assert float('1_0.5e1_0') == 105000000000.0
assert float('1e+5_0') == 1e+50
assert float('1_0_0') == 100.0
assert float('0.1_2') == 0.12
assert float('١٢٣_٤٥٦') == 123456.0     # Nd digits transform before the check

# ===== 3. misplaced underscores keep the usual message =====

expect_ve('_1.5')
expect_ve('1.5_')
expect_ve('1_.5')
expect_ve('1__0')
expect_ve('0.1__2')
expect_ve('1.5e+_1')
expect_ve('inf_')
expect_ve('in_f')
expect_ve('_')

# ===== 4. plain literals and special values are unaffected =====

assert float('1.5') == 1.5
assert float('5.e3') == 5000.0
assert float('1.') == 1.0
assert float('.5') == 0.5
assert float('inf') == float('inf')
assert float('-Infinity') == float('-inf')
assert str(float('nan')) == 'nan'
assert float('+inF') == float('inf')

print("test_float_str_underscore_comma_regression passed")
