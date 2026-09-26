"""Verifies generator state classification on send/throw and the deprecated three-argument throw() signature.

Closed or exhausted generators stop iteration on any send, while the just-started TypeError applies only to a never-started one; throw(type, value, tb) warns with DeprecationWarning and normalizes arguments like CPython gen_throw.

:kind: test
"""

# Regression: generator state classification and the deprecated
# throw() signature.
# Face A: a closed/exhausted generator stops iteration immediately on
# send (including non-None values); the "just-started" TypeError only
# applies to a never-started generator.
# Face B: throw(type, value, tb) warns with DeprecationWarning and
# normalizes like CPython gen_throw — the value instantiates the
# exception class (an exception instance wins as-is), a non-None
# traceback argument is rejected, and an instance type rejects a
# separate value.
import warnings

def make():
    def g():
        try:
            yield 1
        except ValueError as e:
            print("thrown", e.args)
            yield 2
    return g()

# Face A: closed generators stop iteration on send
gen = make()
gen.close()
try:
    gen.send(1)
    raise AssertionError("expected StopIteration")
except StopIteration:
    pass

gen = make()
gen.close()
assert next(gen, "STOP") == "STOP"

gen = make()
next(gen)
gen.close()
try:
    gen.send(1)
    raise AssertionError("expected StopIteration")
except StopIteration:
    pass

# a live just-started generator still rejects non-None sends
fresh = make()
try:
    fresh.send(1)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "can't send non-None value to a just-started generator"

# Face B: (type, value, tb) form
gen = make()
next(gen)
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    assert gen.throw(ValueError, "val", None) == 2
    assert len(w) == 1
    assert w[0].category.__name__ == "DeprecationWarning"
    assert str(w[0].message) == "the (type, exc, tb) signature of throw() is deprecated, use the single-arg signature instead."

gen = make()
next(gen)
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    assert gen.throw(ValueError, ValueError("v"), None) == 2
    assert len(w) == 1

gen = make()
next(gen)
assert gen.throw(ValueError, "w") == 2

gen = make()
next(gen)
try:
    gen.throw(ValueError, "v", "notb")
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "throw() third argument must be a traceback object"

gen = make()
next(gen)
try:
    gen.throw(ValueError("i"), "x")
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "instance exception may not have a separate value"

for args in [(42, "v"), (42, "v", None)]:
    gen = make()
    next(gen)
    try:
        gen.throw(*args)
        raise AssertionError("expected TypeError")
    except TypeError as e:
        assert str(e) == "exceptions must be classes or instances deriving from BaseException, not int"

# throwing into a closed generator propagates the exception
gen = make()
next(gen)
gen.close()
try:
    gen.throw(ValueError)
    raise AssertionError("expected ValueError")
except ValueError:
    pass

# the single-arg form stays warning-free
gen = make()
next(gen)
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    assert gen.throw(ValueError("x")) == 2
    assert len(w) == 0

print("test_generator_close_state_throw3 passed")
