"""Verifies that break, continue, and return jumping out of async with and async for await __aexit__ and unwind runtime handler state correctly.

Covers break and continue across async with, return from async with, break out of async for, and break crossing async with under try/finally; coroutines are driven manually with send(None).

:kind: test
"""

# Regression test: break/continue/return crossing async with / async for.
#
# async-with bodies keep [aexit, manager] resident under a runtime handler
# record and async-for wraps its body in an await-watching record. Jumps out
# used to leak both. The compiler now inlines the awaited __aexit__(None,
# None, None) sequence (async-with) and drops the await record (async-for)
# before the jump, like CPython's ASYNC_WITH/FOR fblock unwinding.
#
# Coroutines are driven manually with send(None): no asyncio in PySharp.

class ACM:
    def __init__(self, tag):
        self.tag = tag
    async def __aenter__(self):
        print('enter', self.tag)
        return self.tag
    async def __aexit__(self, *e):
        print('aexit', self.tag)
        return False

def run(coro):
    try:
        coro.send(None)
    except StopIteration:
        pass

# 1. break out of async with inside a for loop: __aexit__ awaited inline
async def m1():
    for i in range(3):
        async with ACM(i):
            if i == 1:
                break
    print('done-async-break')
run(m1())

# 2. continue crossing async with in a while loop
async def m2():
    n = 0
    while n < 3:
        n += 1
        async with ACM(n):
            if n == 2:
                continue
        print('tail', n)
    print('done-async-continue')
run(m2())

# 3. return from async with inside a loop: value survives the awaited exit
async def m3():
    for i in range(3):
        async with ACM(i):
            if i == 1:
                return i * 10
    return -1
coro = m3()
try:
    coro.send(None)
    assert False, 'must return'
except StopIteration as e:
    value = getattr(e, 'value', None)
assert value == 10
print('done-async-return')

# 4. break out of async for: the await record and aiter are both dropped
class AIter:
    def __init__(self):
        self.i = 0
    def __aiter__(self):
        return self
    async def __anext__(self):
        if self.i >= 3:
            raise StopAsyncIteration
        self.i += 1
        return self.i

async def m4():
    got = []
    async for x in AIter():
        got.append(x)
        if x == 2:
            break
    assert got == [1, 2]
    async for x in AIter():
        got.append(x + 10)
        if x == 2:
            break
    assert got == [1, 2, 11, 12]
    print('done-async-for-break')
run(m4())

# 5. nested: async with under try/finally, break crossing both
async def m5():
    for i in range(3):
        try:
            async with ACM('n%d' % i):
                if i == 1:
                    break
        finally:
            print('fin', i)
    print('done-async-nested')
run(m5())

print("test_async_control_flow passed")
