# Regression: int(s, 0) follows the base-0 literal rules — a
# prefix-less literal starting with '0' is an invalid old-style octal
# unless its parsed value is zero (CPython long_from_string's
# error_if_nonzero), so '08'/'01'/'0_8' raise ValueError while
# '00'/'0_0'/'00_0' stay valid; explicit base 10 keeps accepting
# leading zeros.
def expect_invalid(s, base=0):
    try:
        int(s, base)
        raise AssertionError("expected ValueError for " + repr(s))
    except ValueError as e:
        assert str(e) == "invalid literal for int() with base " + str(base) + ": " + repr(s), (s, str(e))

expect_invalid('08')
expect_invalid('01')
expect_invalid('007')
expect_invalid('0_8')
expect_invalid('00_1')
expect_invalid('-08')
expect_invalid('+08')
expect_invalid(' 08 ')
expect_invalid('08_5')
expect_invalid(b'08')

assert int('00', 0) == 0
assert int('000', 0) == 0
assert int('0', 0) == 0
assert int('0_0', 0) == 0
assert int('00_0', 0) == 0
assert int('-0', 0) == 0
assert int('0x08', 0) == 8
assert int('0o7', 0) == 7
assert int('12', 0) == 12
assert int('1_0', 0) == 10

# explicit base 10 keeps accepting leading zeros and underscores
assert int('08', 10) == 8
assert int('08') == 8
assert int('0_8', 10) == 8
assert int('08_5', 10) == 85

print("test_int_base0_leading_zero_regression passed")
