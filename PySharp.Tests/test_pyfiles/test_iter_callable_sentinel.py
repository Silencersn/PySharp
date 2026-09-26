"""
iter(callable, sentinel) must return a callable iterator that
repeatedly calls the callable with no args until the result compares equal
to the sentinel (sentinel as the left operand of ==), swallows StopIteration
raised by the callable as a clean exhaustion, propagates other errors, and
stops calling the callable entirely once exhausted.

:kind: test
"""

# basic: call until the sentinel shows up
vals = [1, 2, 3, 0]
it = iter(lambda: vals.pop(0), 0)
assert list(it) == [1, 2, 3], list(it)

# sentinel on the first call yields an empty sequence
assert list(iter(lambda: 0, 0)) == []

# exhaustion is sticky: the callable is no longer invoked afterwards
vals2 = ["a", 0]
calls = []


def popper():
    calls.append(1)
    return vals2.pop(0)


it2 = iter(popper, 0)
assert next(it2, "E") == "a"
assert next(it2, "E") == "E"
assert next(it2, "E") == "E"
assert len(calls) == 2, len(calls)

# identity counts as equality before __eq__ runs (CPython fast path)
sentinel = object()
it3 = iter(lambda: sentinel, sentinel)
assert next(it3, "E") == "E"

# StopIteration from the callable exhausts silently
def stopper():
    raise StopIteration


assert list(iter(stopper, None)) == []
assert next(iter(stopper, None), "E") == "E"

# other errors from the callable propagate untouched
def raiser():
    raise ValueError("boom")


try:
    list(iter(raiser, None))
    raise AssertionError("no error propagated")
except ValueError as e:
    assert str(e) == "boom", str(e)

# __eq__ is consulted with the sentinel as the LEFT operand
left_hits = []


class Sentinel:
    def __eq__(self, other):
        left_hits.append(type(other).__name__)
        return True


def maker():
    return object()


assert next(iter(maker, Sentinel()), "E") == "E"
assert left_hits == ["object"], left_hits

# a False __eq__ result keeps the iteration going
class Never:
    def __eq__(self, other):
        return False


values = [0, 1, 2]
it5 = iter(lambda: values.pop(0), Never())
assert next(it5) == 0
assert next(it5) == 1
assert next(it5) == 2

# and the callable's own errors still propagate afterwards
try:
    next(it5)
    raise AssertionError("no error propagated")
except IndexError:
    pass

# a non-callable first argument gets CPython's message
try:
    iter(42, 0)
    raise AssertionError("non-callable accepted")
except TypeError as e:
    assert str(e) == "iter(v, w): v must be callable", str(e)

# the result is its own iterator and has __next__
it4 = iter(lambda: 0, 0)
assert iter(it4) is it4
assert hasattr(it4, "__next__")
assert type(it4).__name__ == "callable_iterator"
assert type(it4).__module__ == "builtins"

# the two-arg form does not shadow the single-arg protocol form
assert list(iter([1, 2])) == [1, 2]
assert list(iter("ab")) == ["a", "b"]

print("test_iter_callable_sentinel passed")
