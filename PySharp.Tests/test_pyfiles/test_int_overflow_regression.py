"""
Regression tests for .NET OverflowException leaks on huge ints
(issues #6/#7/#9/#10/#18/#27).

CPython 3.14 semantics (verified): out-of-range operations raise Python
exceptions (ValueError / OverflowError / IndexError); PySharp used to leak
bare .NET exceptions (OverflowException / ArgumentOutOfRangeException) that
bypass try/except.
"""

# ===== #6 integer shift =====
assert 1 << 10 == 1024

try:
    1 << -1                       # negative shift count -> ValueError
    assert False
except ValueError:
    pass

try:
    1 << 2**100                   # shift count too large -> OverflowError
    assert False
except OverflowError:
    pass

# ===== #7 str * n =====
assert 'a' * 3 == 'aaa'
assert 'a' * -1 == ''             # negative repeat -> empty string

try:
    'a' * 10**20                  # too large -> OverflowError
    assert False
except OverflowError:
    pass

# ===== #9 range slicing =====
r = range(0, 10**20)[::1]         # must not crash
assert r[0] == 0
assert r[-1] == 10**20 - 1

# ===== #18 huge index =====
try:
    [1, 2, 3][10**400]            # -> IndexError
    assert False
except IndexError:
    pass

try:
    [1, 2, 3][-10**400]           # -> IndexError
    assert False
except IndexError:
    pass

try:
    'abc'[10**400]                # -> IndexError
    assert False
except IndexError:
    pass

# ===== #27 round with huge ndigits =====
assert round(1.5, 10**100) == 1.5

# ===== #10 chr with huge arg =====
assert chr(65) == 'A'
assert len(chr(0x10FFFF)) == 1

try:
    chr(2**40)                    # -> ValueError: chr() arg not in range
    assert False
except ValueError:
    pass

print("test_int_overflow_regression passed")
