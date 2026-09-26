"""
resuming a finished coroutine must raise
RuntimeError("cannot reuse already awaited coroutine") instead of a silent
StopIteration, which made a double await look like a normal return.

CPython 3.14 reference (gen_send_ex2's FRAME_STATE_FINISHED branch):
    a finished coroutine raises the reuse error for send/throw/await,
    while gen_close is the only silent path; finished generators and async
    generators keep their own StopIteration / StopAsyncIteration semantics.

Previously the exhausted coroutine took the generator path, so the second
await produced a bogus None result and the logic error went undiagnosed.

:kind: test
"""


def err(fn):
    try:
        return ('ok', fn())
    except Exception as ex:
        return (type(ex).__name__, str(ex))


async def work():
    return 42


def fresh():
    return work()


REUSE = ('RuntimeError', 'cannot reuse already awaited coroutine')

# a finished coroutine reports the reuse error on every further resume
co = fresh()
assert err(lambda: co.send(None)) == ('StopIteration', '42')
assert err(lambda: co.send(None)) == REUSE
assert err(lambda: co.send(None)) == REUSE

# a non-None send is rejected with the reuse error, not the just-started
# TypeError that only applies to a never-started coroutine
co = fresh()
assert err(lambda: co.send(None))[0] == 'StopIteration'
assert err(lambda: co.send(7)) == REUSE

# close() stays silent but still consumes the coroutine
co = fresh()
assert err(lambda: co.send(None))[0] == 'StopIteration'
assert co.close() is None
assert err(lambda: co.send(None)) == REUSE

# closing a never-started coroutine consumes it too, and close is idempotent
co = fresh()
assert co.close() is None
assert co.close() is None
assert err(lambda: co.send(None)) == REUSE

# throw() into an exhausted coroutine reports reuse instead of the thrown
# exception; the same holds after an explicit close()
co = fresh()
assert err(lambda: co.send(None))[0] == 'StopIteration'
assert err(lambda: co.throw(ValueError('boom'))) == REUSE

co = fresh()
assert err(lambda: co.send(None))[0] == 'StopIteration'
assert co.close() is None
assert err(lambda: co.throw(ValueError('boom'))) == REUSE

# awaiting the same coroutine twice reaches the same error
async def double_await():
    c = fresh()
    first = await c
    second = await c
    return (first, second)


d = double_await()
assert err(lambda: d.send(None)) == REUSE

# awaiting a coroutine that was closed before the await
async def await_closed():
    c = fresh()
    c.close()
    return await c


d2 = await_closed()
assert err(lambda: d2.send(None)) == REUSE

# normal awaits keep returning values, and a suspended coroutine resumes
async def chained():
    return (await fresh()) * 2


ch = chained()
assert err(lambda: ch.send(None)) == ('StopIteration', '84')

async def suspended():
    return await fresh()


s = suspended()
assert err(lambda: s.send(None)) == ('StopIteration', '42')

# a never-started coroutine keeps the just-started and throw semantics
co = fresh()
assert err(lambda: co.send(5)) == (
    'TypeError', "can't send non-None value to a just-started coroutine")
assert co.close() is None
co = fresh()
assert err(lambda: co.throw(ValueError('w'))) == ('ValueError', 'w')
assert co.close() is None

# generators are untouched: an exhausted one still just stops iterating
def gen():
    yield 1
    return 9


g = gen()
assert next(g) == 1
assert err(lambda: g.send(None))[0] == 'StopIteration'
assert err(lambda: next(g))[0] == 'StopIteration'
assert err(lambda: g.send(5))[0] == 'StopIteration'
assert g.close() is None

g2 = gen()
assert g2.close() is None
assert err(lambda: g2.send(None))[0] == 'StopIteration'

# async generators keep raising StopAsyncIteration once exhausted
async def agen():
    yield 1


async def async_gen_checks():
    a = agen()
    assert await a.asend(None) == 1
    try:
        await a.asend(None)
        assert False, "expected StopAsyncIteration"
    except StopAsyncIteration:
        pass
    try:
        await a.asend(7)
        assert False, "expected StopAsyncIteration"
    except StopAsyncIteration:
        pass


ag = async_gen_checks()
try:
    ag.send(None)
except StopIteration:
    pass

print("test_coroutine_reuse passed")
