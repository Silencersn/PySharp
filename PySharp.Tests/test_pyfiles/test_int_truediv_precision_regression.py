# Regression: int / int true division follows CPython long_true_divide
# exactly — the quotient rounds half-to-even in the integer domain with
# the shift clamped at DBL_MIN_EXP so subnormal results round once;
# 0 / negative keeps a -0.0 sign; a quotient that rounds up to 2**1024
# raises OverflowError instead of returning inf. All pinned values are
# CPython 3.14 ground truth.

import math

# --- the issue's faces: int/int must agree with float/float ---
assert repr(10 / 3) == "3.3333333333333335"
assert repr(1 / 3) == "0.3333333333333333"
assert repr(2 / 3) == "0.6666666666666666"
assert repr(7 / 2) == "3.5"
assert repr(10**30 / 3) == "3.333333333333333e+29"
assert 10 / 3 == 10 / 3.0
assert 1 / 3 == 1 / 3.0
assert 1 / 49 * 49 == 1 / 49.0 * 49.0

# --- signed zero: 0 / b takes the sign of b ---
assert repr(0 / 3) == "0.0"
assert repr(0 / -3) == "-0.0"
assert repr(0 / -1) == "-0.0"
assert repr(0 / -(2**100)) == "-0.0"
assert repr(False / -3) == "-0.0"
assert math.copysign(1.0, 0 / -3) == -1.0
assert math.copysign(1.0, 0 / 3) == 1.0

# --- underflow to signed zero at and below the halfway point ---
assert repr(1 / 10**400) == "0.0"
assert repr(-1 / 10**400) == "-0.0"
assert repr(1 / 2**1075) == "0.0"
assert repr(-1 / 2**1075) == "-0.0"
assert repr(7 / 2**1080) == "0.0"

# --- subnormal/near-boundary results round exactly once (pinned hex) ---
for N, expect in [
    (2**51 - 1, "0x0.7ffffffffffffp-1022"),
    (2**51 + 3, "0x0.8000000000003p-1022"),
    (2**50 + 7, "0x0.4000000000007p-1022"),
    (2**49 + 11, "0x0.200000000000bp-1022"),
    (2**52 - 3, "0x0.ffffffffffffdp-1022"),
]:
    got = ((4 * N + 1) * 2**30 + 1) / 2**1106
    assert got.hex() == expect, (N, got.hex(), expect)

for num, den, expect in [
    (2**76 + 1, 2**1090, "0x1.0000000000000p-1014"),
    (3, 2**1074, "0x0.0000000000003p-1022"),
    (2**53 - 1, 2**1075, "0x1.0000000000000p-1022"),
    (2**52 + 1, 2**1074, "0x1.0000000000001p-1022"),
]:
    assert (num / den).hex() == expect, (num, den, (num / den).hex(), expect)

# --- overflow: a quotient rounding up to 2**1024 is an error ---
for a, b in [
    (2**1025, 2),
    (2**1024, 1),
    (2**1024 - 2**970, 1),
    (2**1025 - 1, 2),
    (2**1025 + 1, 2),
    (3 * 2**1024 - 3 * 2**970 + 1, 3),
    (10**500, 3),
    (-(10**500), 7),
]:
    try:
        a / b
        raise AssertionError("expected OverflowError for %r / %r" % (a, b))
    except OverflowError as e:
        assert str(e) == "integer division result too large for a float", str(e)

# ...while the quotient just below stays finite at 1 ulp under the top
assert repr((3 * 2**1024 - 3 * 2**970 - 1) / 3) == "1.7976931348623157e+308"

# --- half-even ties ---
assert repr((2**54 + 3) / 2) == "9007199254740994.0"
assert repr((2**54 + 5) / 2) == "9007199254740994.0"
assert repr((2**55 + 1) / 2) == "1.8014398509481984e+16"
assert repr(1 / 2**54) == "5.551115123125783e-17"
assert repr(3 / 2**54) == "1.6653345369377348e-16"
assert repr(2**60 / 3) == "3.843071682022823e+17"
assert repr((2**60 + 1) / 3) == "3.843071682022823e+17"

# --- exact quotients keep exact conversion ---
assert repr(2**200 / 2**47) == "1.141798154164768e+46"
assert (2**200 / 2**47).hex() == float(2**153).hex()
assert repr((2**54 + 1) / 1) == "1.8014398509481984e+16"
assert repr(10**400 / 10**100) == "1e+300"

# --- neighboring paths untouched ---
assert 10 // 3 == 3 and 10 % 3 == 1 and divmod(10, 3) == (3, 1)
assert (-7) // 2 == -4 and (-7) % 2 == 1
assert repr(10 / 3.0) == "3.3333333333333335"
try:
    1 / 0
    raise AssertionError("expected ZeroDivisionError")
except ZeroDivisionError as e:
    assert str(e) == "division by zero", str(e)

print("test_int_truediv_precision_regression passed")
