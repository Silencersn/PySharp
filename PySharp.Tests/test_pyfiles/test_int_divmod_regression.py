"""
Regression: integer divmod() must use floor semantics like CPython's
l_divmod (Objects/longobject.c) - quotient floored, remainder with the
divisor's sign - and must raise ZeroDivisionError on a zero divisor.
PySharp used to return .NET's truncated DivRem results (divmod(-7, 3)
== (-2, -1)) and crashed with an unhandled DivideByZeroException on
divmod(7, 0).

Also pins the // shortcut path: with a negative dividend smaller in
magnitude than the divisor, the truncated quotient is 0 and must be
corrected to -1 ((-1) // 2**32 == -1). PySharp used to return 0.
"""

def check(a, b):
    q, r = divmod(a, b)
    assert (q, r) == (a // b, a % b), (a, b, q, r)
    assert b * q + r == a, (a, b, q, r)
    if b > 0:
        assert 0 <= r < b, (a, b, q, r)
    else:
        assert b < r <= 0, (a, b, q, r)


# red cases: sign combinations of divmod (floor semantics)
assert divmod(-7, 3) == (-3, 2)
assert divmod(7, -3) == (-3, -2)
assert divmod(-7, -3) == (2, -1)
assert divmod(7, 3) == (2, 1)

# red cases: large operands
assert divmod(-10**20, 3) == (-33333333333333333334, 2)
assert divmod(-1, 2**64) == (-1, 18446744073709551615)

# red cases: // shortcut path (|a| < |b|, opposite signs)
assert (-1) // 2**32 == -1
assert (-5) // 2**64 == -1

# invariants across sign combinations
for a in (-10, -7, -3, 0, 3, 7, 10):
    for b in (-3, -2, 2, 3):
        check(a, b)


# red case: zero divisor must raise ZeroDivisionError (was a native
# DivideByZeroException crash); bools share the integer path
for b in (0, False):
    try:
        divmod(7, b)
    except ZeroDivisionError as e:
        assert "division by zero" in str(e), str(e)
    else:
        assert False, "should raise ZeroDivisionError"


# guards: already-correct forms
assert divmod(True, True) == (1, 0)
assert divmod(-7.5, 2) == (-4.0, 0.5)
assert divmod(3, 2.0) == (1.0, 1.0)
assert -7 // 3 == -3 and -7 % 3 == 2
assert (-1) % 2**32 == 2**32 - 1
assert 4 // 7 == 0
assert divmod(-8, 4) == (-2, 0)          # exact division stays exact

print("test_int_divmod_regression passed")
