# test_async_generator: Verify async def + yield support

# Test 1: Basic async generator with async for
async def gen():
    yield 10
    yield 20

async def test_basic():
    results = []
    async for x in gen():
        results.append(x)
    assert results == [10, 20], f"Expected [10, 20], got {results}"
    print("OK: basic async generator")

# Test 2: asend(None) = __anext__()
async def test_asend_none():
    g = gen()
    r1 = await g.asend(None)
    assert r1 == 10, f"Expected 10, got {r1}"
    r2 = await g.asend(None)
    assert r2 == 20, f"Expected 20, got {r2}"
    try:
        await g.asend(None)
        assert False, "Should raise StopAsyncIteration"
    except StopAsyncIteration:
        pass
    print("OK: asend(None)")

# Test 3: yield receives sent value
async def test_send_value():
    async def gen2():
        val = yield 1
        yield val * 2

    g = gen2()
    r1 = await g.asend(None)
    assert r1 == 1, f"Expected 1, got {r1}"
    r2 = await g.asend(21)
    assert r2 == 42, f"Expected 42, got {r2}"
    try:
        await g.asend(None)
        assert False, "Should raise StopAsyncIteration"
    except StopAsyncIteration:
        pass
    print("OK: send value into async generator")

# Test 4: athrow
async def test_athrow():
    async def gen3():
        try:
            yield 1
        except ZeroDivisionError:
            yield 999

    g = gen3()
    r1 = await g.asend(None)
    assert r1 == 1
    r2_coro = g.athrow(ZeroDivisionError)
    # athrow returns an awaitable; drive it
    try:
        r2_coro.send(None)
    except StopIteration as e:
        val = e.args[0] if e.args else None
        assert val == 999, f"Expected 999, got {val}"
    else:
        assert False, "Should raise StopIteration"

    try:
        await g.asend(None)
        assert False, "Should raise StopAsyncIteration"
    except StopAsyncIteration:
        pass
    print("OK: athrow")

async def main():
    await test_basic()
    await test_asend_none()
    await test_send_value()
    await test_athrow()
    print("\nAll async generator tests passed")

# Drive manually: no asyncio event loop in PySharp
coro = main()
try:
    coro.send(None)
except StopIteration:
    pass
