"""Wide-slot dunders resolve by call-time MRO lookup with descriptor binding.

:kind: test

The 13 wide dunders (names CPython carries no slotdef for) must behave like
CPython's _PyObject_LookupSpecial consumers: the name is resolved on the
type's MRO at call time and bound through the descriptor protocol, so a
plain callable (or a staticmethod) assigned to the name is invoked *without*
the instance, while a plain function is invoked with it.
"""


def expect_raises(exc_type, fn):
    try:
        fn()
    except exc_type as exc:
        return exc
    raise AssertionError("expected " + exc_type.__name__)


# --- __format__ binding shapes -------------------------------------------
class FormatPrint:
    __str__ = lambda self: "X"
    __format__ = print


# a plain callable receives only the format spec
expect_raises(TypeError, lambda: format(FormatPrint(), ""))
expect_raises(TypeError, lambda: format(FormatPrint(), "x"))
expect_raises(TypeError, lambda: "{}".format(FormatPrint()))


class FormatStatic:
    __format__ = staticmethod(lambda spec: "S:" + spec)


assert format(FormatStatic(), "q") == "S:q"
assert f"{FormatStatic():w}" == "S:w"
assert "{}".format(FormatStatic()) == "S:"


class FormatFunc:
    def __format__(self, spec):
        return "F<" + spec + ">"


assert format(FormatFunc(), "z") == "F<z>"
assert f"{FormatFunc():k}" == "F<k>"


# None assigned to the name: the lookup still finds it and the invocation
# fails with the usual non-callable TypeError
class FormatNone:
    __format__ = None


err = expect_raises(TypeError, lambda: format(FormatNone(), ""))
assert "NoneType" in str(err), err

# --- with-statement binding shapes ---------------------------------------
class EnterPrint:
    __enter__ = print
    __exit__ = lambda self, *exc: False


# a plain callable __enter__ is invoked with no arguments at all
with EnterPrint() as value:
    assert value is None

enter_log = []


class ExitPrint:
    __enter__ = lambda self: self
    __exit__ = print


# a plain callable __exit__ receives exactly the three exception values
with ExitPrint():
    pass
assert enter_log == []


class ExitSuppress:
    __enter__ = lambda self: self
    __exit__ = lambda self, *exc: True


with ExitSuppress():
    raise ValueError("boom")


class ExitFalse:
    entered = []

    def __enter__(self):
        self.entered.append(1)
        return self

    def __exit__(self, *exc):
        return False


def _raise_in_with():
    with ExitFalse():
        raise ValueError("re-raised")


err = expect_raises(ValueError, _raise_in_with)
assert err.args[0] == "re-raised"


class AsyncPair:
    async def __aenter__(self):
        return "entered"

    async def __aexit__(self, *exc):
        return False


# coroutines are driven manually with send(None) (no event loop in the
# diff harness, matching the other async fixtures)
async def probe_async_with():
    async with AsyncPair() as value:
        return value


coro = probe_async_with()
try:
    while True:
        coro.send(None)
except StopIteration as stop:
    assert stop.value == "entered"

# --- __reversed__ binding -------------------------------------------------
class ReversedPrint:
    __reversed__ = print


# invoked with no arguments; the non-iterable return fails in reversed()
err = expect_raises(TypeError, lambda: list(reversed(ReversedPrint())))
assert "iterable" in str(err) or "iter" in str(err), err


class ReversedOK:
    def __reversed__(self):
        return iter([3, 2, 1])


assert list(reversed(ReversedOK())) == [3, 2, 1]

# --- __missing__ dynamics -------------------------------------------------
class DynMissing(dict):
    pass


DynMissing.__missing__ = lambda self, key: ("m", key)
assert DynMissing()["zz"] == ("m", "zz")
del DynMissing.__missing__
err = expect_raises(KeyError, lambda: DynMissing()["zz"])
assert err.args[0] == "zz"


class StaticMissing(dict):
    def __missing__(self, key):
        return "fallback:" + key


assert StaticMissing()["a"] == "fallback:a"
assert StaticMissing({"x": 1})["x"] == 1
assert StaticMissing({"x": 1})["y"] == "fallback:y"

# --- round dispatch -------------------------------------------------------
class RoundBare:
    def __round__(self):
        return 7


assert round(RoundBare()) == 7
err = expect_raises(TypeError, lambda: round(RoundBare(), 1))
assert "1 positional argument but 2 were given" in str(err), err


class RoundNDigits:
    def __round__(self, n=None):
        return 8


assert round(RoundNDigits()) == 8
assert round(RoundNDigits(), 1) == 8


class NoRound:
    pass


err = expect_raises(TypeError, lambda: round(NoRound()))
assert "doesn't define __round__ method" in str(err), err

print("test_wide_slot_binding OK")
