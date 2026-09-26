"""Names the __aexit__ slot when an async with target supports neither async context manager method.

Mirroring CPython 3.14, the compiler probes __aexit__ before __aenter__
for async with, so an object with neither slot reports
'(missed __aexit__ method)'; objects missing exactly one slot keep their
own slot name. Coroutines are driven manually with send(None) to
simulate an event loop.

:kind: test
"""


class NoAEnter:
    def __aexit__(self, *args):
        return False


class NoAExit:
    def __aenter__(self):
        return self


class NoABoth:
    pass


def missing_slot_name(cls):
    async def probe():
        try:
            async with cls():
                pass
        except TypeError as e:
            return str(e)
        raise AssertionError("async with over " + cls.__name__ + " did not raise TypeError")

    coro = probe()
    try:
        while True:
            coro.send(None)
    except StopIteration as stop:
        return stop.value


assert missing_slot_name(NoAEnter) == "'NoAEnter' object does not support the asynchronous context manager protocol (missed __aenter__ method)"
assert missing_slot_name(NoAExit) == "'NoAExit' object does not support the asynchronous context manager protocol (missed __aexit__ method)"
assert missing_slot_name(NoABoth) == "'NoABoth' object does not support the asynchronous context manager protocol (missed __aexit__ method)"

print("test_async_with_missing_context_manager_slots passed")
