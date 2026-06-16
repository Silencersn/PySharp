# test_async_for: Verify compilation and runtime execution of async for / async with
# Runtime tests use manual send(None) to drive coroutines, simulating an event loop

# ============================================================
# Helper: manually drive a coroutine to completion
# ============================================================
def run(coro):
    try:
        while True:
            coro.send(None)
    except StopIteration:
        pass


# ============================================================
# Runtime test — async for basic functionality
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

_runtime_result = None

async def test_runtime_async_for_basic():
    global _runtime_result
    total = 0
    async for x in AsyncRange(5):
        total += x
    _runtime_result = total

run(test_runtime_async_for_basic())
assert _runtime_result == 10, f"test_runtime_async_for_basic: Expected 10, got {_runtime_result}"
print("test_runtime_async_for_basic passed")


# ============================================================
# Runtime test — async for with break
# ============================================================

_runtime_result = None

async def test_runtime_async_for_break():
    global _runtime_result
    total = 0
    async for x in AsyncRange(10):
        total += x
        if x >= 3:
            break
    _runtime_result = total

run(test_runtime_async_for_break())
assert _runtime_result == 6, f"test_runtime_async_for_break: Expected 6, got {_runtime_result}"
print("test_runtime_async_for_break passed")


# ============================================================
# Runtime test — async for with else clause
# ============================================================

_runtime_result = None

async def test_runtime_async_for_else():
    global _runtime_result
    saw_else = False
    async for x in AsyncRange(0):
        pass
    else:
        saw_else = True
    _runtime_result = saw_else

run(test_runtime_async_for_else())
assert _runtime_result == True, f"test_runtime_async_for_else: Expected True, got {_runtime_result}"
print("test_runtime_async_for_else passed")


# ============================================================
# Runtime test — else clause should NOT execute on break
# ============================================================

_runtime_result = None

async def test_runtime_async_for_else_not_on_break():
    global _runtime_result
    saw_else = False
    async for x in AsyncRange(5):
        break
    else:
        saw_else = True
    _runtime_result = saw_else

run(test_runtime_async_for_else_not_on_break())
assert _runtime_result == False, f"test_runtime_async_for_else_not_on_break: Expected False, got {_runtime_result}"
print("test_runtime_async_for_else_not_on_break passed")


# ============================================================
# Runtime test — nested async for
# ============================================================

_runtime_result = None

async def test_runtime_nested_async_for():
    global _runtime_result
    total = 0
    async for x in AsyncRange(3):
        async for y in AsyncRange(3):
            total += x * y
    _runtime_result = total

run(test_runtime_nested_async_for())
assert _runtime_result == 9, f"test_runtime_nested_async_for: Expected 9, got {_runtime_result}"
print("test_runtime_nested_async_for passed")


# ============================================================
# Runtime test — async with basic functionality
# ============================================================

class AsyncCM:
    def __init__(self, expected=42):
        self.expected = expected
        self.enter_called = False
        self.exit_called = False
        self.exit_args = None
    async def __aenter__(self):
        self.enter_called = True
        return self.expected
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        self.exit_called = True
        self.exit_args = (exc_type, exc_val, exc_tb)
        return False

_runtime_result = None

async def test_runtime_async_with_basic():
    global _runtime_result
    cm = AsyncCM(42)
    async with cm as val:
        assert val == 42, f"Expected 42, got {val}"
    assert cm.enter_called, "__aenter__ not called"
    assert cm.exit_called, "__aexit__ not called"
    # Normal exit: exc_type should be None
    assert cm.exit_args[0] is None, f"Expected None exc_type, got {cm.exit_args[0]}"
    _runtime_result = True

run(test_runtime_async_with_basic())
assert _runtime_result == True, "test_runtime_async_with_basic failed"
print("test_runtime_async_with_basic passed")


# ============================================================
# Runtime test — async with multiple context managers
# ============================================================

class TrackingCM:
    def __init__(self, name, val):
        self.name = name
        self.val = val
        self.enter_called = False
        self.exit_called = False
    async def __aenter__(self):
        self.enter_called = True
        return self.val
    async def __aexit__(self, exc_type, exc_val, exc_tb):
        self.exit_called = True
        return False

_runtime_result = None

async def test_runtime_async_with_multi():
    global _runtime_result
    cm_a = TrackingCM("a", 1)
    cm_b = TrackingCM("b", 2)
    async with cm_a as a, cm_b as b:
        assert a == 1, f"Expected a=1, got {a}"
        assert b == 2, f"Expected b=2, got {b}"
    assert cm_a.enter_called, "cm_a.__aenter__ not called"
    assert cm_a.exit_called, "cm_a.__aexit__ not called"
    assert cm_b.enter_called, "cm_b.__aenter__ not called"
    assert cm_b.exit_called, "cm_b.__aexit__ not called"
    _runtime_result = True

run(test_runtime_async_with_multi())
assert _runtime_result == True, "test_runtime_async_with_multi failed"
print("test_runtime_async_with_multi passed")


# ============================================================
# Runtime test — break inside async with
# ============================================================

_runtime_result = None

async def test_runtime_async_with_break():
    global _runtime_result
    cm = AsyncCM(10)
    async with cm as val:
        assert val == 10, f"Expected 10, got {val}"
        # break inside async with should still call __aexit__
    assert cm.exit_called, "__aexit__ not called after body"
    _runtime_result = True

run(test_runtime_async_with_break())
assert _runtime_result == True, "test_runtime_async_with_break failed"
print("test_runtime_async_with_break passed")


# ============================================================
# Compilation test — verify async for/with syntax is compilable inside async def
# ============================================================

# Compilation test: basic async for syntax
async def test_compile_async_for():
    class FakeAsyncIter:
        def __aiter__(self):
            return self
        def __anext__(self):
            return self._get_next()
        async def _get_next(self):
            return 42

    result = 0
    async for item in FakeAsyncIter():
        result = item
        break
    assert result == 42, f"Expected 42, got {result}"

print("test_compile_async_for compiled successfully")

# Compilation test: async for with else clause
async def test_compile_async_for_else():
    class EmptyAsyncIter:
        def __aiter__(self):
            return self
        def __anext__(self):
            raise StopAsyncIteration

    saw_else = False
    async for item in EmptyAsyncIter():
        pass
    else:
        saw_else = True
    assert saw_else, "Expected else clause to execute"

print("test_compile_async_for_else compiled successfully")

# Compilation test: basic async with syntax
async def test_compile_async_with():
    class FakeAsyncCM:
        async def __aenter__(self):
            return 42
        async def __aexit__(self, exc_type, exc_val, exc_tb):
            return False

    async with FakeAsyncCM() as val:
        assert val == 42, f"Expected 42, got {val}"

print("test_compile_async_with compiled successfully")

# Compilation test: async with multiple context managers
async def test_compile_async_with_multi():
    class SimpleAsyncCM:
        def __init__(self, val):
            self.val = val
        async def __aenter__(self):
            return self.val
        async def __aexit__(self, exc_type, exc_val, exc_tb):
            return False

    async with SimpleAsyncCM(1) as a, SimpleAsyncCM(2) as b:
        assert a == 1, f"Expected 1, got {a}"
        assert b == 2, f"Expected 2, got {b}"

print("test_compile_async_with_multi compiled successfully")

# Compilation test: nested async for
async def test_compile_nested_async_for():
    class FakeRange:
        def __init__(self, n):
            self.n = n
            self.i = 0
        def __aiter__(self):
            return self
        def __anext__(self):
            if self.i >= self.n:
                raise StopAsyncIteration
            val = self.i
            self.i += 1
            return self._ret(val)
        async def _ret(self, val):
            return val

    total = 0
    async for x in FakeRange(3):
        async for y in FakeRange(3):
            total += x * y
    assert total == 9, f"Expected 9, got {total}"

print("test_compile_nested_async_for compiled successfully")

print("test_async_for passed")
