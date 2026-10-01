"""Error-message conformance for the async iteration/context-manager
protocols: the four 'async for' templates and the '__aenter__'/'__aexit__'
ones carry CPython's bare tp_name (no quotes around the type name), and a
non-awaitable result is attributed to its protocol context (__anext__,
__aenter__, __aexit__) instead of falling back to the generic
"'{0}' object can't be awaited".

CPython 3.14 reference: Python/bytecodes.c GET_AITER,
Python/ceval.c _PyEval_GetANext and _PyEval_FormatAwaitableError.

:kind: test
"""

# test_async_protocol_error_messages: bare type names in async protocol
# errors and per-context attribution of non-awaitable results


def drive(coro):
    try:
        while True:
            coro.send(None)
    except StopIteration as e:
        return e.value
    except BaseException as e:
        return ("EXC", type(e).__name__, str(e))


class NoAIter:
    pass


class NoANext:
    def __aiter__(self):
        return self


class BadNext:
    def __aiter__(self):
        return self

    def __anext__(self):
        return 42


class AIterNoANext:
    def __aiter__(self):
        return 42


class BadEnter:
    def __aenter__(self):
        return 42

    def __aexit__(self, *exc):
        return False


class BadExit:
    async def __aenter__(self):
        return self

    def __aexit__(self, *exc):
        return 42


# A: async for over an object without __aiter__
async def case_missing_aiter():
    async for x in NoAIter():
        pass


# B: __anext__ returns a non-awaitable object
async def case_invalid_anext_result():
    async for x in BadNext():
        pass


# C: __aiter__ returns an object without __anext__
async def case_aiter_returns_no_anext():
    async for x in AIterNoANext():
        pass


# D: __aiter__ returns self, which lacks __anext__
async def case_self_without_anext():
    async for x in NoANext():
        pass


# I: __aenter__ returns a non-awaitable object
async def case_non_awaitable_aenter():
    async with BadEnter():
        pass


# J: __aexit__ returns a non-awaitable object
async def case_non_awaitable_aexit():
    async with BadExit():
        pass


# K (control): a plain await keeps the generic message, quotes included
async def case_plain_await():
    await 42


assert drive(case_missing_aiter()) == (
    "EXC", "TypeError", "'async for' requires an object with __aiter__ method, got NoAIter")
assert drive(case_invalid_anext_result()) == (
    "EXC", "TypeError", "'async for' received an invalid object from __anext__: int")
assert drive(case_aiter_returns_no_anext()) == (
    "EXC", "TypeError", "'async for' received an object from __aiter__ that does not implement __anext__: int")
assert drive(case_self_without_anext()) == (
    "EXC", "TypeError", "'async for' received an object from __aiter__ that does not implement __anext__: NoANext")
assert drive(case_non_awaitable_aenter()) == (
    "EXC", "TypeError", "'async with' received an object from __aenter__ that does not implement __await__: int")
assert drive(case_non_awaitable_aexit()) == (
    "EXC", "TypeError", "'async with' received an object from __aexit__ that does not implement __await__: int")
assert drive(case_plain_await()) == (
    "EXC", "TypeError", "'int' object can't be awaited")

print("test_async_protocol_error_messages passed")
