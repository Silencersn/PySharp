class AsyncRange:
    def __init__(self, n):
        self.n = n
        self.i = 0
    def __aiter__(self):
        return self
    async def __anext__(self):
        if self.i >= self.n:
            raise StopAsyncIteration
        val = self.i
        self.i += 1
        return val

def run():
    async def inner():
        result = [x async for x in AsyncRange(3)]
        return result
    coro = inner()
    try:
        coro.send(None)
    except StopIteration:
        pass

run()

# Test 2: async for over an async generator expression
# This exercises PyAsyncGeneratorASendObject through __anext__()
def test_consume_async_gen():
    async def consumer():
        ag = (x async for x in AsyncRange(3))
        results = []
        async for x in ag:
            results.append(x)
        return results

    coro = consumer()
    try:
        coro.send(None)
    except StopIteration:
        pass

test_consume_async_gen()

