"""
Regression: async constructs outside their required context must be
rejected with SyntaxError at compile time, like CPython. PySharp used to
accept (and even fully execute) the following forms:

    def f():                 # sync function
        async for i in it(): ...      -> 'async for' outside async function
        async with cm(): ...          -> 'async with' outside async function

    async def g():           # async generator
        yield 1
        return 5                      -> 'return' with value in async generator

CPython 3.14 reference (Python/symtable.c:79,82, Python/codegen.c:2202).

Each illegal form is checked through compile() so this file itself stays
valid. Guards pin the already-consistent rejections ('await' outside
async function, 'yield from' inside async function) and the legal async
forms that must keep compiling.
"""

SRC_ASYNC_FOR_IN_SYNC = (
    "class AIter:\n"
    "    def __aiter__(self):\n"
    "        return self\n"
    "    async def __anext__(self):\n"
    "        raise StopAsyncIteration\n"
    "def f():\n"
    "    async for i in AIter():\n"
    "        pass\n"
)

SRC_ASYNC_WITH_IN_SYNC = (
    "class ACM:\n"
    "    async def __aenter__(self):\n"
    "        return self\n"
    "    async def __aexit__(self, *e):\n"
    "        return False\n"
    "def f():\n"
    "    async with ACM():\n"
    "        pass\n"
)

SRC_ASYNC_GEN_RETURN_VALUE = "async def g():\n    yield 1\n    return 5\n"


def expect_syntax_error(src, fragment):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + fragment
    except SyntaxError as e:
        assert fragment in str(e), str(e)


# red cases: missing context validation
expect_syntax_error(SRC_ASYNC_FOR_IN_SYNC, "outside async function")
expect_syntax_error(SRC_ASYNC_WITH_IN_SYNC, "outside async function")
expect_syntax_error(SRC_ASYNC_GEN_RETURN_VALUE, "async generator")

# guards: already-consistent rejections must stay rejected
expect_syntax_error("def f():\n    await x()\n", "outside async function")
expect_syntax_error("async def f(it):\n    x = yield from it\n",
                    "inside async function")

# guards: legal async forms must keep compiling
compile("async def g():\n    yield 1\n    return\n", "<test>", "exec")
compile("async def f(it):\n    async for i in it():\n        pass\n",
        "<test>", "exec")
compile("async def f(cm):\n    async with cm():\n        pass\n",
        "<test>", "exec")

print("test_async_context_validation_regression passed")
