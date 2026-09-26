"""Names the __exit__ slot when a with target supports neither context manager method.

The probe order decides the diagnostic: CPython 3.14 emits LOAD_SPECIAL
__exit__ before __enter__ (Python/codegen.c codegen_with_inner) and
_LOAD_SPECIAL raises at the first missing slot, so an object with neither
slot reports '(missed __exit__ method)'. Objects missing exactly one slot
keep their own slot name in the message.

:kind: test
:background: CPython 3.14 probes __exit__ first; mirroring the probe order
    in the compiler keeps the both-slots-missing TypeError aligned.
"""


class NoEnter:
    def __exit__(self, *args):
        return False


class NoExit:
    def __enter__(self):
        return self


class NoBoth:
    pass


def missing_slot_name(cls):
    try:
        with cls():
            pass
    except TypeError as e:
        return str(e)
    raise AssertionError("with over " + cls.__name__ + " did not raise TypeError")


assert missing_slot_name(NoEnter) == "'NoEnter' object does not support the context manager protocol (missed __enter__ method)"
assert missing_slot_name(NoExit) == "'NoExit' object does not support the context manager protocol (missed __exit__ method)"
assert missing_slot_name(NoBoth) == "'NoBoth' object does not support the context manager protocol (missed __exit__ method)"

print("test_with_missing_context_manager_slots passed")
