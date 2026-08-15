"""
Regression: list.index(x, start) must clamp an out-of-range negative start to
0 (CPython), instead of leaking a bare .NET ArgumentOutOfRangeException that
Python try/except cannot catch.

CPython 3.14 reference:
    [1, 2, 3].index(1, -100)      == 0
    [1, 2, 3].index(3, -100, 100) == 2
    (1, 2, 3).index(1, -100)      == 0   (tuple, already correct)
"""

assert [1, 2, 3].index(1, -100) == 0
assert [1, 2, 3].index(3, -100, 100) == 2
assert [1, 2, 3].index(2, -2) == 1
assert [1, 2, 3].index(1, -100, 2) == 0
assert (1, 2, 3).index(1, -100) == 0

# End out-of-range is also clamped.
assert [1, 2, 3].index(3, 0, 100) == 2

# Empty search range still raises ValueError (sanity check).
try:
    [1, 2, 3].index(1, 0, -100)
    assert False, "[1, 2, 3].index(1, 0, -100) should raise ValueError"
except ValueError:
    pass

print("test_list_index_start_regression passed")
