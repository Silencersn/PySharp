"""
Regression: runtime decimal conversions between int and str must raise
ValueError beyond 4300 digits (the runtime half of the CVE-2020-10735
guard), like CPython. PySharp used to accept both directions
unconditionally.

CPython 3.14 reference (Objects/longobject.c, shared
_PY_LONG_DEFAULT_MAX_STR_DIGITS = 4300):
    int("9" * 4300)      -> fine
    int("9" * 4301)      -> ValueError (Exceeds the limit ...)
    str(10 ** 5000)      -> ValueError (5001 digits)
    str(10**4300 - 1)    -> fine (4300 digits)
    int("f" * 5000, 16)  -> fine (power-of-two bases are not limited)

This pins the Builtins/runtime direction; the compile-time literal limit
is a separate fix (see the int literal max digits regression test).
"""

def expect_value_error(fn):
    try:
        fn()
        assert False, "should raise ValueError"
    except ValueError:
        pass


# str -> int: boundary 4300 / 4301
v = int("9" * 4300)
assert isinstance(v, int) and v % 10 == 9

expect_value_error(lambda: int("9" * 4301))

# int -> str: boundary 4300 / >4300 digits
s = str(10 ** 4300 - 1)
assert len(s) == 4300 and s[0] == "9"

expect_value_error(lambda: str(10 ** 5000))

# guard: power-of-two bases are not limited
h = int("f" * 5000, 16)
assert h > 0

print("test_int_str_conversion_limit_regression passed")
