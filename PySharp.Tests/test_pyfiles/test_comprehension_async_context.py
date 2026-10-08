"""
Async context rules for comprehensions and generator expressions

:kind: test
"""
def check(src):
    try:
        compile(src, "<s>", "exec")
        return "OK"
    except SyntaxError as e:
        return str(e.msg)

# a genexp containing await or async for is an implicit async generator,
# legal to create in any context
assert check("list(x async for x in it)\n") == "OK"
assert check("list(await g() for x in [1])\n") == "OK"
assert check("def h():\n    return list([x async for x in it] for y in [1])\n") == "OK"

# an inlined comprehension containing await or async for becomes async as
# a whole and must sit in an async function
assert check("[x for x in [1] if await g()]\n") == "asynchronous comprehension outside of an asynchronous function"
assert check("{x for x in [1] if await g()}\n") == "asynchronous comprehension outside of an asynchronous function"
assert check("{x: x for x in [1] if await g()}\n") == "asynchronous comprehension outside of an asynchronous function"
assert check("def h():\n    return [x for x in [1] if await g()]\n") == "asynchronous comprehension outside of an asynchronous function"
assert check("[[x async for x in it] for y in [1]]\n") == "asynchronous comprehension outside of an asynchronous function"

# a bare await keeps its own two-level wording
assert check("def h():\n    return await g()\n") == "'await' outside async function"
assert check("await g()\n") == "'await' outside function"

# runtime: async generator expressions iterate through async for, and a
# nested await mixes with the async clause
async def agen():
    yield 1
    yield 2

async def adouble(x):
    return x * 2

async def main():
    r1 = [x async for x in agen()]
    assert r1 == [1, 2]
    r2 = [item async for item in (x async for x in agen())]
    assert r2 == [1, 2]
    r3 = [item async for item in (x async for x in agen() if x > 1)]
    assert r3 == [2]
    r4 = [(x, await adouble(x)) async for x in agen()]
    assert r4 == [(1, 2), (2, 4)]

coro = main()
try:
    coro.send(None)
except StopIteration:
    pass
