"""
Regression: a failing `close` attribute lookup on a `yield from` delegate is
reported through the unraisable channel instead of propagating out of the
caller's `close()`.

CPython 3.14 reference (Objects/genobject.c gen_close_iter): a delegate that
is not an exact generator/coroutine is closed through its `close` attribute,
looked up with PyObject_GetOptionalAttr(); a failing lookup is reported with
PyErr_FormatUnraisable("Exception ignored while closing generator %R", yf)
and gen_close keeps going - the delegate is skipped and the generator still
closes. Only a `close()` call that itself raises propagates out of the
caller's `close()` (gen_close_iter returns -1).

Previously PySharp propagated the lookup failure out of `close()`, which
interrupted the caller's cleanup and left the generator suspended.
"""

events = []


class LookupRaises:
    """yield from delegate whose close attribute cannot be looked up."""

    def __iter__(self):
        return self

    def __next__(self):
        return 1

    def __getattr__(self, name):
        events.append(('lookup', name))
        raise RuntimeError('boom-getattr:' + name)


def delegated():
    yield from LookupRaises()


# close() reports the failing lookup out of band and still closes the
# generator: the delegate is skipped, nothing is delivered to it.
g = delegated()
assert next(g) == 1
assert g.close() is None
assert events == [('lookup', 'close')], events
assert next(g, 'STOP') == 'STOP'
assert g.close() is None
assert events == [('lookup', 'close')], events


# the skipped close() still lets the delegating frame run its own cleanup
def delegated_cleanup():
    try:
        yield from LookupRaises()
    finally:
        events.append('cleanup')


events = []
g = delegated_cleanup()
assert next(g) == 1
assert g.close() is None
assert events == [('lookup', 'close'), 'cleanup'], events
assert next(g, 'STOP') == 'STOP'


# the GeneratorExit is still delivered after the report, so a frame that
# swallows it keeps the usual "ignored GeneratorExit" error
def delegated_ignores():
    try:
        yield from LookupRaises()
    except GeneratorExit:
        yield 2


events = []
g = delegated_ignores()
assert next(g) == 1
try:
    g.close()
except RuntimeError as error:
    assert str(error) == 'generator ignored GeneratorExit', error
else:
    raise AssertionError('close() did not report the ignored GeneratorExit')
assert events == [('lookup', 'close')], events
assert next(g, 'STOP') == 'STOP'


# a never-started generator never touches the delegate
events = []
g = delegated()
assert g.close() is None
assert events == [], events


# a delegate without a close attribute is skipped as well
class NoClose:
    def __iter__(self):
        return self

    def __next__(self):
        return 1


def delegated_no_close():
    yield from NoClose()


g = delegated_no_close()
assert next(g) == 1
assert g.close() is None
assert next(g, 'STOP') == 'STOP'


# an AttributeError from __getattr__ means "no close attribute" too
class LookupAttributeError:
    def __iter__(self):
        return self

    def __next__(self):
        return 1

    def __getattr__(self, name):
        raise AttributeError(name)


def delegated_attribute_error():
    yield from LookupAttributeError()


g = delegated_attribute_error()
assert next(g) == 1
assert g.close() is None
assert next(g, 'STOP') == 'STOP'


# only a close() that raises propagates to the caller
class CloseRaises:
    def __iter__(self):
        return self

    def __next__(self):
        return 1

    def close(self):
        raise RuntimeError('boom-close')


def delegated_close_raises():
    yield from CloseRaises()


g = delegated_close_raises()
assert next(g) == 1
try:
    g.close()
except RuntimeError as error:
    assert str(error) == 'boom-close', error
else:
    raise AssertionError('close() swallowed the delegate close error')


# a delegate close whose return value is not None is still ignored
class CloseReturnsValue:
    def __init__(self):
        self.closed = 0

    def __iter__(self):
        return self

    def __next__(self):
        return 1

    def close(self):
        self.closed += 1
        return 'ignored'


delegate = CloseReturnsValue()


def delegated_close_value():
    yield from delegate


g = delegated_close_value()
assert next(g) == 1
assert g.close() is None
assert delegate.closed == 1
assert next(g, 'STOP') == 'STOP'


# an exact sub-generator is closed through gen_close, not through the attribute
sub_events = []


def sub():
    try:
        yield 1
    finally:
        sub_events.append('sub-cleanup')


def outer():
    yield from sub()


g = outer()
assert next(g) == 1
assert g.close() is None
assert sub_events == ['sub-cleanup'], sub_events


# throw() keeps propagating a failing throw lookup instead of reporting it
class ThrowLookupRaises:
    def __iter__(self):
        return self

    def __next__(self):
        return 1

    def close(self):
        pass

    def __getattr__(self, name):
        raise RuntimeError('boom-throw:' + name)


def delegated_throw():
    yield from ThrowLookupRaises()


g = delegated_throw()
assert next(g) == 1
try:
    g.throw(ValueError('x'))
except RuntimeError as error:
    assert str(error) == 'boom-throw:throw', error
else:
    raise AssertionError('throw() swallowed the failing throw lookup')
assert g.close() is None

print("test_yield_from_close_lookup_unraisable_regression passed")
