"""
Regression: PEP 479 — a StopIteration escaping a generator frame must be
replaced by RuntimeError('generator raised StopIteration') with the original
exception as __cause__, instead of being mistaken for normal exhaustion.
Coroutines and async generators use their own messages. StopIteration that
is caught inside the body, a normal return, or a throw into an already
exhausted generator must keep the pre-existing behavior.

CPython 3.14 reference:
    def bad():
        yield 1
        raise StopIteration
    next(bad()) twice -> RuntimeError: generator raised StopIteration
                         (__cause__ is the StopIteration instance)
    exhausted_gen.throw(StopIteration) -> StopIteration (frame never entered)
    gen.throw(StopIteration) caught in body -> generator continues
    coroutine body raising StopIteration -> RuntimeError: coroutine raised StopIteration
    async gen body raising StopIteration -> RuntimeError: async generator raised StopIteration

Previously the raw StopIteration escaped every generator frame, silently
terminating the generator like a normal exhaustion.
"""


def bad_gen():
    yield 1
    raise StopIteration


class MyStop(StopIteration):
    pass


def subcls_gen():
    yield 1
    raise MyStop


def ret_gen():
    yield 1
    return 42


def loop():
    i = 0
    while True:
        yield i
        i += 1


def close_gen():
    try:
        yield 1
    finally:
        raise StopIteration


def caught_gen():
    try:
        yield 1
    except StopIteration:
        pass
    yield 2


# body raise -> RuntimeError with __cause__, across StopIteration subclasses
g = bad_gen()
next(g)
try:
    next(g)
except RuntimeError as e:
    assert str(e) == 'generator raised StopIteration'
    assert type(e.__cause__).__name__ == 'StopIteration'
else:
    raise AssertionError('StopIteration escaped generator without conversion')

g = subcls_gen()
next(g)
try:
    next(g)
except RuntimeError as e:
    assert str(e) == 'generator raised StopIteration'
    assert type(e.__cause__).__name__ == 'MyStop'
else:
    raise AssertionError('StopIteration subclass escaped without conversion')

# gen.throw(StopIteration) left uncaught -> RuntimeError as well
g = loop()
next(g)
try:
    g.throw(StopIteration)
except RuntimeError as e:
    assert str(e) == 'generator raised StopIteration'
    assert type(e.__cause__).__name__ == 'StopIteration'
else:
    raise AssertionError('thrown StopIteration escaped without conversion')

# close() over a body that raises StopIteration -> RuntimeError
g = close_gen()
next(g)
try:
    g.close()
except RuntimeError as e:
    assert str(e) == 'generator raised StopIteration'
else:
    raise AssertionError('close() swallowed a body-raised StopIteration')

# for-loop and yield-from must propagate the RuntimeError, not end quietly
def drive_for():
    for _ in bad_gen():
        pass

try:
    drive_for()
except RuntimeError:
    pass
else:
    raise AssertionError('for loop ended quietly on body-raised StopIteration')

def drive_yieldfrom():
    yield from bad_gen()

g = drive_yieldfrom()
next(g)
try:
    next(g)
except RuntimeError:
    pass
else:
    raise AssertionError('yield from ended quietly on body-raised StopIteration')

# behavior that must NOT change
g = ret_gen()
next(g)
try:
    next(g)
except StopIteration as e:
    assert e.value == 42
else:
    raise AssertionError('return 42 must end the generator normally')

g = caught_gen()
next(g)
assert next(g) == 2

g = bad_gen()
next(g)
try:
    next(g)
except RuntimeError:
    pass
try:
    g.throw(StopIteration)
except StopIteration:
    pass
else:
    raise AssertionError('throw on exhausted generator must re-raise as-is')

try:
    next(iter([]))
except StopIteration:
    pass
else:
    raise AssertionError('plain iterators keep raising StopIteration')

# coroutine / async generator messages
async def acor():
    raise StopIteration

async def drive_coro():
    await acor()

coro = drive_coro()
try:
    coro.send(None)
except RuntimeError as e:
    assert str(e) == 'coroutine raised StopIteration'
else:
    raise AssertionError('coroutine StopIteration not converted')

async def agen_fn():
    yield 1
    raise StopIteration

async def drive_agen():
    ag = agen_fn()
    await ag.__anext__()
    await ag.__anext__()

coro = drive_agen()
try:
    coro.send(None)
except RuntimeError as e:
    assert str(e) == 'async generator raised StopIteration'
else:
    raise AssertionError('async generator StopIteration not converted')

print("test_pep479_generator_stopiteration_regression passed")
