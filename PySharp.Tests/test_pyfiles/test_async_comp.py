# test_async_comp: Verify compilation of async comprehensions
# Only defines functions, does NOT execute them

# ============================================================
# Helper async-iterable
# ============================================================
class AsyncRange:
    """An async-iterable integer range"""
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

# ============================================================
# Valid: async for in list comprehension inside async function
# ============================================================
async def test_list_comp():
    result = [x async for x in AsyncRange(5)]

# ============================================================
# Valid: async for in set comprehension inside async function
# ============================================================
async def test_set_comp():
    result = {x async for x in AsyncRange(5)}

# ============================================================
# Valid: async for in dict comprehension inside async function
# ============================================================
async def test_dict_comp():
    result = {k: k async for k in AsyncRange(5)}

# ============================================================
# Valid: async for in generator expression inside async function
# ============================================================
async def test_genexp():
    gen = (x async for x in AsyncRange(5))

# ============================================================
# Valid: mixed async and sync for clauses
# ============================================================
async def test_mixed():
    result = [x async for x in AsyncRange(3) for y in range(2)]

# ============================================================
# Valid: async for with if clause
# ============================================================
async def test_async_for_with_if():
    result = [x async for x in AsyncRange(10) if x % 2 == 0]

# ============================================================
# Valid: nested async comprehensions
# ============================================================
async def test_nested():
    result = [x async for x in [y async for y in AsyncRange(3)]]

# ============================================================
# Valid: await inside comprehension in async function
# ============================================================
async def test_await_in_comp():
    async def fetch(x):
        return x
    result = [await fetch(x) for x in range(3)]

# ============================================================
# Valid: async for in genexp (lazy) — allowed even at module level
# ============================================================
_gen = (x async for x in AsyncRange(3))
