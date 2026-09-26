"""Verifies len() bounds a __len__ result like CPython's slot_sq_length: any negative result raises the >= 0 ValueError, an oversized positive raises OverflowError with the index-sized message, a bool result normalizes to an exact int, and the bool()/not fallback propagates the OverflowError.

:kind: test
"""

# Regression: len() bounds the __len__ result like CPython's
# slot_sq_length — the sign check runs first (any negative result is
# the >= 0 ValueError), then the Py_ssize_t conversion raises
# OverflowError for a positive result beyond the index range. A bool
# result is normalized to an exact int via _PyNumber_Index.
def make(v):
    class X:
        def __len__(self):
            return v
    return X()

for desc, v, expect in [
    ("10**30", 10**30, "overflow"),
    ("2**63", 2**63, "overflow"),
    ("2**63-1", 2**63 - 1, 9223372036854775807),
    ("2**31", 2**31, 2147483648),
]:
    try:
        result = len(make(v))
        assert expect != "overflow" and result == expect, desc
    except OverflowError as e:
        assert expect == "overflow", desc
        assert str(e) == "cannot fit 'int' into an index-sized integer", desc

for v in [-1, -10**30, -2**63]:
    try:
        len(make(v))
        raise AssertionError("expected ValueError for " + repr(v))
    except ValueError as e:
        assert str(e) == "__len__() should return >= 0", repr(v)

# a bool result is normalized to an exact int
class T:
    def __len__(self):
        return True

assert len(T()) == 1
assert type(len(T())).__name__ == "int"

# the bool()/not fallback through __len__ propagates the OverflowError
class Big:
    def __len__(self):
        return 10**30

try:
    bool(Big())
    raise AssertionError("expected OverflowError")
except OverflowError:
    pass
try:
    not Big()
    raise AssertionError("expected OverflowError")
except OverflowError:
    pass

# plain containers are unaffected
assert len([1, 2, 3]) == 3
assert len("abcd") == 4

print("test_len_bounds passed")
