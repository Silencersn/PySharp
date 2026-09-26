"""
int() must accept any Unicode decimal digit (Nd) like CPython,
for base 10 and every other base, keeping CPython's exact error messages
(they quote the original string) and the digit-count limit semantics.

:kind: test
"""

# base 10 through the plain and explicit-base entry points
assert int('\u0661\u0662\u0663') == 123
assert int('\uff11\uff12\uff13', 10) == 123
assert int('1\u0662\u0663') == 123

# whitespace, sign and underscore interactions survive
assert int(' \u0661\u0662\u0663 ') == 123
assert int('-\u0664\u0662') == -42
assert int('+3') == 3
assert int('\u0661_\u0662') == 12

# every base: digits map to their decimal values before base parsing
assert int('\u0661\u0662\u0663', 16) == 291
assert int('\u0661\u0662\u0663', 36) == 1371
assert int('\u0661\u0662\u0663', 0) == 123
assert int('0x\u0661\u0662', 16) == 18
assert int('\u0661_\u0662', 16) == 18
assert int('\u06f0\u06f1') == 1
assert int('\u06f0') == 0

# digits at or above the base are invalid
try:
    int('\u0661\u0662\u0663', 2)
    raise AssertionError("base 2 accepted 123")
except ValueError as e:
    assert str(e) == "invalid literal for int() with base 2: '\u0661\u0662\u0663'", str(e)
try:
    int('\u0669', 8)
    raise AssertionError("base 8 accepted 9")
except ValueError as e:
    assert str(e) == "invalid literal for int() with base 8: '\u0669'", str(e)

# only Nd counts: Numeric_Type=Digit and Numeric characters stay invalid
try:
    int('\u00b2')
    raise AssertionError("superscript accepted")
except ValueError as e:
    assert str(e) == "invalid literal for int() with base 10: '\u00b2'", str(e)
try:
    int('\u00bd')
    raise AssertionError("vulgar fraction accepted")
except ValueError as e:
    assert str(e) == "invalid literal for int() with base 10: '\u00bd'", str(e)

# invalid tails keep the original string in the message
try:
    int('\u0661\u0662\u0663\u00e9')
    raise AssertionError("invalid tail accepted")
except ValueError as e:
    assert str(e) == "invalid literal for int() with base 10: '\u0661\u0662\u0663\u00e9'", str(e)

# non-breaking-space padding is trimmed whitespace, not a digit
assert int('\u00a0123\u00a0') == 123

# the digit-count limit still applies to Unicode digits
try:
    int('\u0661' * 4301)
    raise AssertionError("digit limit not enforced")
except ValueError as e:
    assert str(e).startswith("Exceeds the limit (4300 digits)"), str(e)

# long runs parse consistently across scripts
assert int('\u0661' * 30, 8) == int('1' * 30, 8)

print("test_int_unicode_digits passed")
