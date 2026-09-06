"""
Regression: next(x) on a non-iterator must raise
TypeError: '<type>' object is not an iterator, pointing at the argument
itself, like CPython's builtin_next (Python/bltinmodule.c). PySharp used
to misattribute the error to a nonexistent iter() call ("iter()
returned non-iterator of type '...'"), misleading users into checking
iter()'s return value.

Also pins the __iter__-result validation that keeps the messages
consistent: iter() must reject an object whose __iter__ returns a
non-iterator ("iter() returned non-iterator of type '...'"), and a for
loop over such an object must fail with the same message (CPython fails
it at the iter() stage, before the first __next__).
"""

class NoNext:
    pass


class BadIter:
    def __iter__(self):
        return 42


class HasNextOnly:
    def __next__(self):
        raise StopIteration


def expect_type_error(fn, message_part):
    try:
        fn()
    except TypeError as e:
        assert message_part in str(e), str(e)
    else:
        assert False, "should raise TypeError"


# red cases: the message must point at the argument, not at iter()
expect_type_error(lambda: next(42), "'int' object is not an iterator")
expect_type_error(lambda: next([1, 2]), "'list' object is not an iterator")
expect_type_error(lambda: next(NoNext()), "'NoNext' object is not an iterator")
# the default must not swallow the TypeError for a non-iterator
expect_type_error(lambda: next(42, "d"), "'int' object is not an iterator")

# red case: __iter__ returning a non-iterator must be rejected with the
# iter() message, named after the returned value
expect_type_error(
    lambda: iter(BadIter()), "iter() returned non-iterator of type 'int'")
expect_type_error(
    lambda: [x for x in BadIter()], "iter() returned non-iterator of type 'int'")


# guards
try:
    next(HasNextOnly())
except StopIteration:
    pass
else:
    assert False, "a __next__-only object must be a valid iterator"

assert list(iter([1, 2])) == [1, 2]

it = iter("ab")
assert next(it) == "a" and next(it) == "b"

class SelfIter:
    def __iter__(self):
        return self

    def __next__(self):
        raise StopIteration

assert list(SelfIter()) == []

print("test_next_non_iterator_regression passed")
