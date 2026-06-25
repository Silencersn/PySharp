# Verify aiter() / anext() / compile(bytes) behavior against CPython

def run(coro):
    try:
        while True:
            coro.send(None)
    except StopIteration:
        pass

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

# ===== aiter() =====
ait = aiter(AsyncRange(3))
assert type(ait).__name__ == "AsyncRange", f"Got {type(ait).__name__}"
assert hasattr(ait, '__anext__')
print("aiter() type: OK")

# ===== aiter() TypeError =====
try:
    aiter(42)
    assert False, "Should raise TypeError"
except TypeError as e:
    expected = "'int' object is not an async iterable"
    assert str(e) == expected, f"Expected: {expected!r}, Got: {str(e)!r}"
    print(f"aiter(42) error matches CPython: {e}")

# ===== anext() without default =====
async def test_anext_no_default():
    ait = aiter(AsyncRange(3))
    assert await anext(ait) == 0
    assert await anext(ait) == 1
    assert await anext(ait) == 2
    try:
        await anext(ait)
        assert False, "Should raise StopAsyncIteration"
    except StopAsyncIteration:
        pass
    print("anext() no default: OK")

# ===== anext() with default =====
async def test_anext_with_default():
    ait = aiter(AsyncRange(2))
    a = anext(ait, -1)
    assert type(a).__name__ == "anext_awaitable", f"Got {type(a).__name__}"
    assert await a == 0
    assert await anext(ait, -1) == 1
    assert await anext(ait, -1) == -1
    a = anext(ait, "EOF")
    assert type(a).__name__ == "anext_awaitable", f"Got {type(a).__name__}"
    assert await a == "EOF"
    print("anext() with default: OK")

# ===== anext() TypeError =====
class NoANext:
    def __aiter__(self):
        return self

try:
    anext(NoANext())
    assert False, "Should raise TypeError"
except TypeError as e:
    expected = "'NoANext' object is not an async iterator"
    assert str(e) == expected, f"Expected: {expected!r}, Got: {str(e)!r}"
    print(f"anext(no_anext) error matches CPython: {e}")

# ===== compile(bytes) =====
code = compile(b"x = 42", "<bytes>", "exec")
assert type(code).__name__ == "code"
ns = {}
exec(code, ns)
assert ns["x"] == 42
print("compile(bytes) exec: OK")

result = eval(b"1 + 2")
assert result == 3
print("eval(bytes): OK")

exec(b"y = 100")
assert y == 100
print("exec(bytes): OK")

code_eval = compile(b"2 * 3", "<bytes>", "eval")
result = eval(code_eval)
assert result == 6
print("compile(bytes, 'eval'): OK")

code_single = compile(b"z = 7", "<bytes>", "single")
exec(code_single)
assert z == 7
print("compile(bytes, 'single'): OK")

run(test_anext_no_default())
run(test_anext_with_default())

print("\n=== All PySharp checks match CPython ===")
