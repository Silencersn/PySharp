"""
async generator aclose() must return the async_generator_athrow
awaitable and defer the cleanup to the await, like CPython (PEP 525).

CPython 3.14 reference (Objects/genobject.c async_gen_aclose/athrow_send):
    aclose() itself runs nothing; awaiting the returned object injects
    GeneratorExit, and a generator that ignores it reports
    "async generator ignored GeneratorExit". athrow() shares the same
    awaitable type, one awaitable can be driven only once, and a finished
    generator completes the await immediately.

Previously aclose() closed the generator on the spot and returned None, so
the standard `await gen.aclose()` idiom raised
TypeError: 'NoneType' object can't be awaited and the cleanup side effects
ran at call time.

:kind: test
"""


async def drive(obj):
    return await obj


def await_obj(obj):
    d = drive(obj)
    try:
        d.send(None)
    except StopIteration as ex:
        return ('value', ex.value)
    except Exception as ex:
        return ('error', type(ex).__name__ + ': ' + str(ex))
    return ('pending', None)


def err(fn):
    try:
        return ('ok', fn())
    except Exception as ex:
        return (type(ex).__name__, str(ex))


events = []
reuse_error = ('error', 'RuntimeError: cannot reuse already awaited aclose()/athrow()')


async def ag():
    try:
        yield 1
    finally:
        events.append('cleanup')


# aclose() returns the athrow awaitable and runs nothing yet
a = ag()
assert await_obj(a.asend(None)) == ('value', 1)
c = a.aclose()
assert type(c).__name__ == 'async_generator_athrow', type(c).__name__
assert repr(c).split(' at ')[0] == '<async_generator_athrow object'
assert events == [], events
assert await_obj(c) == ('value', None)
assert events == ['cleanup'], events
assert await_obj(a.asend(None))[0] == 'error'
assert await_obj(a.asend(None))[1].startswith('StopAsyncIteration')

# a never-started generator: awaiting aclose() does not run the body
events = []
a = ag()
assert await_obj(a.aclose()) == ('value', None)
assert events == [], events

# an exhausted generator completes the await immediately
events = []
a = ag()
assert await_obj(a.asend(None)) == ('value', 1)
assert await_obj(a.asend(None))[0] == 'error'
assert events == ['cleanup'], events
assert await_obj(a.aclose()) == ('value', None)

# one awaitable can be driven only once
events = []
a = ag()
assert await_obj(a.asend(None)) == ('value', 1)
c = a.aclose()
assert await_obj(c) == ('value', None)
assert await_obj(c) == reuse_error

# non-None send and close() on a not-yet-driven awaitable
a = ag()
c = a.aclose()
assert err(lambda: c.send(5)) == (
    'RuntimeError', "can't send non-None value to a just-started coroutine")
c.close()

a = ag()
c = a.aclose()
assert err(lambda: c.close()) == ('ok', None)

# ignoring GeneratorExit reports the async generator message
async def stubborn():
    try:
        yield 1
    except GeneratorExit:
        yield 2


a = stubborn()
assert await_obj(a.asend(None)) == ('value', 1)
assert await_obj(a.aclose()) == (
    'error', 'RuntimeError: async generator ignored GeneratorExit')

# athrow() shares the awaitable type and still injects its exception
async def catcher():
    try:
        yield 1
    except ValueError:
        yield 99


a = catcher()
assert await_obj(a.asend(None)) == ('value', 1)
t = a.athrow(ValueError)
assert type(t).__name__ == 'async_generator_athrow', type(t).__name__
assert await_obj(t) == ('value', 99)
assert await_obj(t)[0] == 'error'

# athrow() on a finished generator completes the await
a = catcher()
assert await_obj(a.asend(None)) == ('value', 1)
assert await_obj(a.asend(None))[0] == 'error'
assert await_obj(a.athrow(ValueError)) == ('value', None)

# the athrow awaitable is not reusable either
a = catcher()
assert await_obj(a.asend(None)) == ('value', 1)
t = a.athrow(ValueError)
assert await_obj(t) == ('value', 99)
assert await_obj(t)[0] == 'error'
assert await_obj(t) == reuse_error

# plain async iteration is untouched
async def counter():
    yield 1
    yield 2


async def collect():
    out = []
    async for value in counter():
        out.append(value)
    return out


assert await_obj(collect()) == ('value', [1, 2])

print("test_async_generator_aclose passed")
