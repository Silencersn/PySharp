"""
Regression: `x in range` must use CPython's O(1) arithmetic test for int
(and bool) operands instead of enumerating up to range length — huge ranges
such as `10**20 in range(10**30)` used to hang. Non-int operands keep the
generic element-enumeration semantics.

CPython 3.14 reference:
    True in range(1)                 -> True
    1.0 in range(3)                  -> True (float enumerates)
    'a' in range(3)                  -> False
    10**20 in range(10**30)          -> True (instant)
    -1 in range(10**9)               -> False (instant)
    2**63 in range(0, 2**65, 3)      -> False
    3 * 2**62 in range(0, 2**65, 3)  -> True
    4 in range(10, 0, -2)            -> True, 5 -> False, 10 -> True, 0 -> False
    100 in range(5, 100)             -> False (stop excluded)
"""

# bool follows int semantics (True == 1, False == 0)
assert True not in range(1)
assert False in range(1)
assert True not in range(0, 10, 2)
assert False in range(0, 10, 2)
assert 1 in range(0, 2)
assert 0 not in range(1, 5)

# O(1) arithmetic on huge ranges (these complete in milliseconds; the old
# enumeration-based path hung indefinitely)
assert 10**20 in range(10**30)
assert (10**20 + 1) in range(10**30)
assert (10**30) not in range(10**30)
assert -1 not in range(10**9)
assert 2**63 not in range(0, 2**65, 3)
assert 3 * 2**62 in range(0, 2**65, 3)
assert 10**30 + 10**20 in range(10**30, 10**40)
assert 10**20 not in range(0, 10**20)

# positive step boundaries
r = range(1, 10, 3)
assert 1 in r and 4 in r and 7 in r
assert 10 not in r and 2 not in r and 0 not in r

# negative step boundaries
rn = range(10, 0, -2)
assert 10 in rn and 4 in rn and 2 in rn
assert 5 not in rn and 0 not in rn and 11 not in rn

# empty ranges contain nothing
assert 0 not in range(0)
assert 5 not in range(5, 1)
assert 100 not in range(5, 100)
assert -5 in range(-5, 5)
assert -9 in range(1, -100, -10)

# non-int operands keep enumerating (CPython parity)
assert 1.0 in range(3)
assert 1.0 in range(2)
assert 'a' not in range(3)
assert True == (0 in range(0, 1))

print("test_range_contains_regression passed")
