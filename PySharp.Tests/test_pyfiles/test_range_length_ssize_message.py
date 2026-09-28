"""Verifies range's length goes through PyLong_AsSsize_t like CPython's range_length, so a length past ssize_t raises OverflowError with the C ssize_t message instead of the index-sized one. A Python-level __len__ still normalizes through _PyNumber_Index (slot_sq_length) and keeps the index-sized message, and the range paths that consume __len__ carry the new message along.

:kind: test
"""

import operator

SSIZE_MSG = "Python int too large to convert to C ssize_t"
INDEX_MSG = "cannot fit 'int' into an index-sized integer"


def message_of(fn):
    try:
        fn()
    except OverflowError as exc:
        return str(exc)
    raise AssertionError("expected OverflowError")


# range's sq_length/mp_length is the C-level range_length, so both the
# len() builtin and the slot itself overflow with the conversion message
huge = range(10 ** 100)
assert message_of(lambda: len(huge)) == SSIZE_MSG
assert message_of(lambda: huge.__len__()) == SSIZE_MSG

# the paths that read __len__ report with it unchanged
assert message_of(lambda: list(huge)) == SSIZE_MSG
assert message_of(lambda: operator.length_hint(huge)) == SSIZE_MSG

# reverse control: a Python-level __len__ goes through slot_sq_length
# ( _PyNumber_Index ) and keeps the index-sized message
class Big:
    def __len__(self):
        return 2 ** 100


assert message_of(lambda: len(Big())) == INDEX_MSG

# lengths that still fit ssize_t are exact, including near the boundary
assert len(range(0)) == 0
assert len(range(5)) == 5
assert len(range(2 ** 63 - 1)) == 2 ** 63 - 1
assert range(2 ** 63 - 1).__len__() == 2 ** 63 - 1

# nothing else about a huge range changes: __bool__ bypasses __len__, and
# indexing stays exact rather than going through a ssize_t index
assert bool(huge) is True
assert bool(range(0)) is False
assert huge[5] == 5
assert huge[-1] == 10 ** 100 - 1

print("test_range_length_ssize_message passed")
