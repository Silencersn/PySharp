"""
Regression: the awaitable returned by __anext__()/asend() is single use.

CPython 3.14 reference (Objects/genobject.c async_gen_asend_*):
    An AWAITABLE_STATE_INIT object that produced one step - a yield, an
    exhaustion or an error - becomes AWAITABLE_STATE_CLOSED, and driving a
    closed object again raises
    "cannot reuse already awaited __anext__()/asend()".
    async_gen_unwrap_value turns an async yield value into StopIteration and
    returns NULL, so a successful await closes the awaitable as well.

The same state covers the iterator methods (send/throw/next) and close(),
which hands GeneratorExit to the async generator and only surfaces errors
other than StopIteration/StopAsyncIteration/GeneratorExit.

Previously these awaitables carried no state: every drive ran the async
generator again, so a second await silently consumed the next yield value.
"""


def drive(awaitable, value=None):
    """Drives one step with send() and tags the observable outcome."""
    try:
        awaitable.send(value)
    except StopIteration as stopped:
        return ('yield', stopped.value)
    except StopAsyncIteration:
        return ('exhausted', None)
    except BaseException as error:
        return ('error', type(error).__name__ + ': ' + str(error))
    return ('pending', None)


def drive_next(awaitable):
    try:
        next(awaitable)
    except StopIteration as stopped:
        return ('yield', stopped.value)
    except StopAsyncIteration:
        return ('exhausted', None)
    except BaseException as error:
        return ('error', type(error).__name__ + ': ' + str(error))
    return ('pending', None)


def drive_throw(awaitable, error):
    try:
        awaitable.throw(error)
    except StopIteration as stopped:
        return ('yield', stopped.value)
    except StopAsyncIteration:
        return ('exhausted', None)
    except BaseException as raised:
        return ('error', type(raised).__name__ + ': ' + str(raised))
    return ('pending', None)


def run(coroutine):
    try:
        coroutine.send(None)
    except StopIteration as stopped:
        return stopped.value
    raise AssertionError('coroutine did not finish')


reuse_error = 'RuntimeError: cannot reuse already awaited __anext__()/asend()'


async def ag():
    yield 1
    yield 2


# asend() and __anext__() share the awaitable type, and one drive closes it
a = ag()
o = a.asend(None)
assert drive(o) == ('yield', 1), drive(o)
assert drive(o) == ('error', reuse_error), drive(o)
assert drive(o) == ('error', reuse_error), drive(o)

a = ag()
o = a.__anext__()
assert drive(o) == ('yield', 1), drive(o)
assert drive(o) == ('error', reuse_error), drive(o)

# the iterator protocol reaches the same state
a = ag()
o = a.asend(None)
assert drive_next(o) == ('yield', 1), drive_next(o)
assert drive_next(o) == ('error', reuse_error), drive_next(o)

# an exhausted async generator closes the awaitable as well
async def empty():
    if False:
        yield 1


a = empty()
o = a.asend(None)
assert drive(o) == ('exhausted', None), drive(o)
assert drive(o) == ('error', reuse_error), drive(o)

# a raising throw() closes the awaitable
a = ag()
o = a.asend(None)
assert drive_throw(o, ValueError('boom')) == ('error', 'ValueError: boom')
assert drive(o) == ('error', reuse_error), drive(o)

# close() before the first drive reports no error and closes the awaitable
a = ag()
o = a.asend(None)
assert o.close() is None
assert drive(o) == ('error', reuse_error), drive(o)
assert o.close() is None

# close() after a drive is a no-op
a = ag()
o = a.asend(None)
assert drive(o) == ('yield', 1)
assert o.close() is None


# close() hands GeneratorExit to the async generator: one that catches it
# and yields again is not an error for the awaitable (the unwrapped
# StopIteration is swallowed) and the awaitable ends up closed
async def stubborn():
    try:
        yield 1
    except GeneratorExit:
        yield 2


a = stubborn()
o = a.asend(None)
assert drive(o) == ('yield', 1)
assert a.asend(None).close() is None
assert drive(a.asend(None)) == ('exhausted', None)


# the awaitable drives the async generator exactly once, so awaiting the
# same object twice is the reuse error
async def reuse_via_await():
    a = ag()
    o = a.asend(None)
    first = await o
    try:
        await o
    except RuntimeError as error:
        return (first, str(error))
    return (first, None)


assert run(reuse_via_await()) == (1, 'cannot reuse already awaited __anext__()/asend()')


# an explicit non-None send value overrides the value captured by asend()
received = []


async def echo():
    received.append((yield 1))
    yield 2


a = echo()
assert drive(a.asend(None)) == ('yield', 1)
assert drive(a.asend('asend-value'), 'send-value') == ('yield', 2)
assert received == ['send-value'], received


# driving a fresh awaitable while its async generator already runs is
# reported by the awaitable, not by the generator layer
holder = []


async def self_drive():
    try:
        await holder[0].asend(None)
    except RuntimeError as error:
        holder.append('inner: ' + str(error))
    yield 'done'


a = self_drive()
holder.append(a)
o = a.asend(None)
assert drive(o) == ('yield', 'done'), drive(o)
assert holder[1] == 'inner: anext(): asynchronous generator is already running', holder
assert drive(o) == ('error', reuse_error), drive(o)

# a non-None value sent to a just-started async generator stays a TypeError
a = ag()
just_started = drive(a.asend('value'))
assert just_started[0] == 'error', just_started
assert just_started[1].startswith('TypeError: '), just_started

print("test_async_generator_asend_reuse_regression passed")
