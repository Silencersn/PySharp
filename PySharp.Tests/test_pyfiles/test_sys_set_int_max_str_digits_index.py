"""sys.set_int_max_str_digits accepts index-capable objects and rejects the rest with the same error types as CPython.

The limit is per-environment state, but mutating it is still scoped to this
fixture's own environment, so the original value is restored in a finally
block before the fixture exits.

:background: CPython 3.14 Python/sysmodule.c declares the argument with the
    "i" converter (Python/getargs.c), which resolves _PyNumber_Index before
    the C long conversion; non-index objects raise TypeError and values
    beyond C long raise OverflowError. The value validation is unchanged:
    only 0 or values >= 640 are accepted.
:kind: test
"""

import sys


class Ix:
    def __init__(self, value):
        self.value = value

    def __index__(self):
        return self.value


class Plain:
    pass


original = sys.get_int_max_str_digits()
try:
    # __index__ objects are accepted
    sys.set_int_max_str_digits(Ix(700))
    assert sys.get_int_max_str_digits() == 700, sys.get_int_max_str_digits()

    # plain int is unchanged
    sys.set_int_max_str_digits(0)
    assert sys.get_int_max_str_digits() == 0
    sys.set_int_max_str_digits(1000)
    assert sys.get_int_max_str_digits() == 1000

    # value validation is identical for protocol results
    for bad in (Ix(3), 3, Ix(639), 639):
        try:
            sys.set_int_max_str_digits(bad)
        except ValueError:
            pass
        else:
            raise AssertionError("expected ValueError for " + repr(bad))

    # objects without __index__ raise TypeError
    for worse in (Plain(), "700", 700.0):
        try:
            sys.set_int_max_str_digits(worse)
        except TypeError:
            pass
        else:
            raise AssertionError("expected TypeError for " + repr(worse))

    # the failing attempts above left the last good value in place
    assert sys.get_int_max_str_digits() == 1000

    # a lowered limit really gates the conversion it guards
    sys.set_int_max_str_digits(1000)
    v = int("9" * 1000)
    assert v % 10 == 9
    try:
        int("9" * 1001)
    except ValueError:
        pass
    else:
        raise AssertionError("expected ValueError above the lowered limit")
finally:
    sys.set_int_max_str_digits(original)
    assert sys.get_int_max_str_digits() == original

print("test_sys_set_int_max_str_digits_index passed")
