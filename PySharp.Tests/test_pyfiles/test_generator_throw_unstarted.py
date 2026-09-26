"""Verifies that throw() on a never-started generator propagates the exception straight to the caller and closes the generator.

Covers instance and class forms, the deprecated three-arg form, StopIteration and GeneratorExit pass-through, exhausted and suspended generators, genexps, and non-None send / non-exception TypeErrors.

:kind: test
"""

# Regression: gen_throw on a never-started generator propagates the
# exception straight to the caller — no frame is running that could
# catch it — and closes the generator (gi_frame_state -> cleared).
# Resuming the VM for the injection used to pop an empty operand stack
# and crash the process with IndexOutOfRangeException.

def make():
    def gen():
        try:
            yield 1
            yield 2
        except ValueError as e:
            yield ("inner-caught", str(e))
    return gen()

# unstarted throw(instance) propagates and closes the generator
g = make()
try:
    g.throw(ValueError("early"))
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "early"
assert list(g) == []

# unstarted throw(class) behaves the same
g = make()
try:
    g.throw(ValueError)
    raise AssertionError("expected ValueError")
except ValueError:
    pass
assert next(g, "exhausted") == "exhausted"

# unstarted 3-arg throw warns and still propagates
import warnings
g = make()
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    try:
        g.throw(ValueError, "three", None)
        raise AssertionError("expected ValueError")
    except ValueError as e:
        assert str(e) == "three"
    assert len(w) == 1, w

# StopIteration and GeneratorExit propagate as-is (the body never ran)
g = make()
try:
    g.throw(StopIteration())
    raise AssertionError("expected StopIteration")
except StopIteration:
    pass
g = make()
try:
    g.throw(GeneratorExit())
    raise AssertionError("expected GeneratorExit")
except GeneratorExit:
    pass

# after the unstarted throw the generator is closed for every operation
g = make()
try:
    g.throw(ValueError("x"))
except ValueError:
    pass
assert next(g, "exhausted") == "exhausted"
try:
    g.throw(KeyError("again"))
except KeyError as e:
    assert str(e) == "'again'"
assert g.close() is None

# close on an unstarted generator is a no-op that closes it
g = make()
assert g.close() is None
assert next(g, "exhausted") == "exhausted"

# an exhausted generator's throw propagates the exception unchanged
g = make()
next(g)
next(g)
assert next(g, None) is None
try:
    g.throw(ValueError("done"))
except ValueError as e:
    assert str(e) == "done"

# a suspended generator still receives the exception in its body
g = make()
next(g)
assert g.throw(ValueError("mid")) == ("inner-caught", "mid")

# generator expressions share the never-started face
gx = (x for x in range(3))
try:
    gx.throw(ValueError("gx"))
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "gx"
assert list(gx) == []

# non-None send on an unstarted generator stays a TypeError
g = make()
try:
    g.send(1)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "can't send non-None value to a just-started generator"

# non-exception arguments stay a TypeError
g = make()
try:
    g.throw(123)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "exceptions must be classes or instances deriving from BaseException, not int"

print("test_generator_throw_unstarted passed")
