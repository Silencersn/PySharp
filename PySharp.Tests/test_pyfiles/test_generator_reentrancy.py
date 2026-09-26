"""
consuming a generator while it is already executing raises
the catchable ValueError "generator already executing" (CPython
gen_send_ex2's gi_running guard) instead of recursing into the
evaluation loop until a .NET IndexOutOfRangeException kills the
process. The guard covers next/send, throw() and close() resume paths,
and the generator stays usable afterwards.

:kind: test
"""

def err(fn):
    try:
        return ('ok', fn())
    except Exception as ex:
        return (type(ex).__name__, str(ex))

# direct re-entrancy via next()
def g():
    next(it)
    yield 1

it = g()
assert err(lambda: next(it)) == ('ValueError', 'generator already executing')

# re-entrant throw and close hit the same guard
def g3():
    it3.throw(ValueError)
    yield 1

it3 = g3()
assert err(lambda: next(it3)) == ('ValueError', 'generator already executing')

def g4():
    it4.close()
    yield 1

it4 = g4()
assert err(lambda: next(it4)) == ('ValueError', 'generator already executing')

# the guarded generator's state is not corrupted: it finishes normally
assert err(lambda: next(it)) == ('StopIteration', '')

# normal nested generators (different objects) are unaffected
def a():
    yield 1
    yield 2

def b(gen):
    yield next(gen) * 10
    yield next(gen) * 10

ga = a()
assert list(b(ga)) == [10, 20]
assert list(ga) == []

# send protocol intact
def g6():
    x = yield 1
    yield x * 2

g6i = g6()
assert err(lambda: g6i.send(5)) == ('TypeError', "can't send non-None value to a just-started generator")
assert next(g6i) == 1
assert g6i.send(3) == 6

# a generator consumed by an outer consumer while suspended still works
def counter():
    yield 1
    yield 2

def driver():
    c = counter()
    yield next(c)
    yield next(c)

assert list(driver()) == [1, 2]

print("test_generator_reentrancy passed")
