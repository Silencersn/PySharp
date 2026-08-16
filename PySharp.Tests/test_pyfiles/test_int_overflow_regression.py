"""
Regression tests for .NET exception leaks (OverflowException /
ArgumentOutOfRangeException) when operating on huge ints, and for
int/float boundary semantics (silent inf / precision loss).

CPython 3.14 semantics (verified): out-of-range operations raise Python
exceptions (ValueError / OverflowError / IndexError); PySharp used to leak
bare .NET exceptions that bypass try/except.
"""

# ===== integer shift =====
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

# ===== str * n =====
assert 'a' * 3 == 'aaa'
assert 'a' * -1 == ''             # negative repeat -> empty string

try:
    'a' * 10**20                  # too large -> OverflowError
    assert False
except OverflowError:
    pass

# ===== range slicing =====
r = range(0, 10**20)[::1]         # must not crash
assert r[0] == 0
assert r[-1] == 10**20 - 1

# ===== huge index =====
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

# ===== round with huge ndigits =====
assert round(1.5, 10**100) == 1.5

# ===== chr with huge arg =====
assert chr(65) == 'A'
assert len(chr(0x10FFFF)) == 1

try:
    chr(2**40)                    # -> ValueError: chr() arg not in range
    assert False
except ValueError:
    pass

# ===== int/float true division on huge ints =====
assert 10 / 2 == 5.0
assert 10**400 / 10**400 == 1.0

try:
    10**400 / 2                   # -> OverflowError: result too large for float
    assert False
except OverflowError:
    pass

# boundary: |result| just below/above double max (~2^1024)
assert 2**1024 / 3 == 5.992310449541053e+307
assert (2**1024 + 1) / 2 == 8.98846567431158e+307
assert 2**1025 / 3 == 1.1984620899082105e+308
assert 2**1025 / -3 == -1.1984620899082105e+308
assert -2**1025 / 3 == -1.1984620899082105e+308
assert 2**1024 / 2 == 8.98846567431158e+307

try:
    2**1026 / 3                   # -> OverflowError
    assert False
except OverflowError:
    pass

# ===== int/float precise comparison =====
assert 1 == 1.0
assert not (2**53 + 1 == 2.0**53)
assert not (10**400 == float('inf'))
assert 10**400 < float('inf')
assert not (10**400 > float('inf'))
assert 10**400 != float('inf')

# ===== float() on huge int =====
assert float(10**20) == 1e+20

try:
    float(10**400)                # -> OverflowError: int too large to convert
    assert False
except OverflowError:
    pass

# ===== float floor division by huge int =====
assert 5.0 // 2 == 2.0
assert -1.0 // 10**20 == -1.0

try:
    -1.0 // 10**400               # -> OverflowError
    assert False
except OverflowError:
    pass

print("test_int_overflow_regression passed")
