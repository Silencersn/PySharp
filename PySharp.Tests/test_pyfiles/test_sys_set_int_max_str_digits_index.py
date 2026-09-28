"""sys.set_int_max_str_digits accepts index-capable objects and rejects the rest with the same error types as CPython.

The limit is process-wide mutable state shared with the in-process test
host, so every mutation here is restored in a finally block before the
fixture exits.

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
    # __index__ objects are accepted. Every accepted value stays at or above
    # the process default: the limit is process-global mutable state and the
    # corpus runs fixtures concurrently in the test host, so a transient
    # drop below the default could race unrelated fixtures' bigint parsing.
    sys.set_int_max_str_digits(Ix(7000))
    assert sys.get_int_max_str_digits() == 7000, sys.get_int_max_str_digits()

    # plain int is unchanged
    sys.set_int_max_str_digits(0)
    assert sys.get_int_max_str_digits() == 0
    sys.set_int_max_str_digits(6500)
    assert sys.get_int_max_str_digits() == 6500

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
    assert sys.get_int_max_str_digits() == 6500
finally:
    sys.set_int_max_str_digits(original)
    assert sys.get_int_max_str_digits() == original

print("test_sys_set_int_max_str_digits_index passed")
