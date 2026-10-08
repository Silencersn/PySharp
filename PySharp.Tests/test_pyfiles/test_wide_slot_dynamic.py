"""Wide-slot dunders: the type dict is the single source of truth.

:kind: test

With the slot fields gone, a runtime assignment or deletion takes effect on
the very next protocol dispatch (no slot re-wiring involved), deletion of an
own definition falls back through the MRO, and object.__format__ is visible
as the dict-level default exactly like CPython.
"""


def expect_raises(exc_type, fn):
    try:
        fn()
    except exc_type as exc:
        return exc
    raise AssertionError("expected " + exc_type.__name__)


# --- object.__format__ is a real dict entry ------------------------------
assert "__format__" in dir(object())
assert callable(getattr(object(), "__format__"))
assert hasattr(object(), "__format__")
# none of the other wide dunders live on object
for name in ("__round__", "__enter__", "__exit__", "__reversed__", "__missing__", "__set_name__", "__trunc__", "__floor__", "__ceil__", "__complex__"):
    assert not hasattr(object, name), name

# any instance resolves the default through the MRO
class Plain:
    pass


plain = Plain()
assert callable(getattr(Plain(), "__format__"))
assert format(plain, "") == str(plain)
err = expect_raises(TypeError, lambda: format(plain, "x"))
assert "unsupported format string passed to" in str(err), err

# deleting an own definition falls back to the object default
class OwnFormat:
    def __format__(self, spec):
        return "own"


own = OwnFormat()
assert format(own, "") == "own"
del OwnFormat.__format__
assert format(own, "") == str(own)
err = expect_raises(TypeError, lambda: format(own, "x"))
assert "unsupported format string passed to" in str(err), err
# deleting an inherited entry is an AttributeError
err = expect_raises(AttributeError, lambda: delattr(Plain, "__format__"))

# runtime override wins over the base definition immediately
class LateFormat:
    def __format__(self, spec):
        return "early"


late = LateFormat()
assert format(late, "") == "early"
LateFormat.__format__ = lambda self, spec: "late"
assert format(late, "") == "late"
del LateFormat.__format__
assert format(late, "") == str(late)

# subclass resolution follows the MRO at call time
class BaseFmt:
    def __format__(self, spec):
        return "base<" + spec + ">"


class SubFmt(BaseFmt):
    pass


assert f"{SubFmt():q}" == "base<q>"
SubFmt.__format__ = lambda self, spec: "sub<" + spec + ">"
assert f"{SubFmt():q}" == "sub<q>"
del SubFmt.__format__
assert f"{SubFmt():q}" == "base<q>"

# __set_name__ still runs exactly once at class creation, in dict order
log = []


class Hook:
    def __set_name__(self, owner, name):
        log.append((owner.__name__, name))


class Host:
    a = Hook()
    b = Hook()


assert log == [("Host", "a"), ("Host", "b")], log

# a failing __set_name__ fails the class creation
class Bad:
    def __set_name__(self, owner, name):
        raise RuntimeError("no naming")


def _make_boom():
    class Boom:
        x = Bad()
    return Boom


err = expect_raises(RuntimeError, _make_boom)
assert err.args[0] == "no naming"

print("test_wide_slot_dynamic OK")
