"""
Regression: isinstance / issubclass dispatch to __instancecheck__ and
__subclasscheck__.

CPython's object_recursive_isinstance and object_issubclass
(Objects/abstract.c) resolve the hook on the type of the second argument,
bind it to that argument and interpret the call result by its truth;
tuple classinfo recurses element by element, and both builtins fall back to
the plain type check when no hook is found. Without the dispatch a custom
metaclass hook was never called and the builtins answered with the plain
check, silently returning a wrong boolean.

CPython 3.14 reference (Objects/abstract.c object_recursive_isinstance,
object_issubclass, _PyObject_LookupSpecial).
"""

log = []


class Meta(type):
    def __instancecheck__(cls, inst):
        log.append(("instance", cls.__name__, inst))
        return inst == "yes"

    def __subclasscheck__(cls, sub):
        log.append(("subclass", cls.__name__))
        return sub is int


class Check(metaclass=Meta):
    pass


assert isinstance("yes", Check) is True
assert isinstance(3, Check) is False
assert issubclass(int, Check) is True
assert issubclass(str, Check) is False
assert [entry[:2] for entry in log] == [
    ("instance", "Check"),
    ("instance", "Check"),
    ("subclass", "Check"),
    ("subclass", "Check"),
], log


# --- tuple classinfo recurses element by element, nested tuples included ---
log.clear()
assert isinstance("yes", (int, Check)) is True
assert issubclass(int, (str, Check)) is True
# no earlier element matches, so the nested tuple reaches the hook
assert isinstance("yes", (int, (float, Check))) is True
assert len(log) == 3, log


# --- the hook result is taken by its truth, not by identity with True ---
class Truthy(type):
    def __instancecheck__(cls, inst):
        return "yes"


class Falsy(type):
    def __instancecheck__(cls, inst):
        return ""


class CheckTruthy(metaclass=Truthy):
    pass


class CheckFalsy(metaclass=Falsy):
    pass


assert isinstance(1, CheckTruthy) is True
assert isinstance(1, CheckFalsy) is False


# --- an exception raised by the hook propagates ---
class Raising(type):
    def __instancecheck__(cls, inst):
        raise RuntimeError("boom")


class CheckRaising(metaclass=Raising):
    pass


try:
    isinstance(1, CheckRaising)
    raise AssertionError("the hook exception must propagate")
except RuntimeError as e:
    assert str(e) == "boom", str(e)


# --- a hook defined on a plain class is ignored, since its type is type ---
class Plain:
    def __instancecheck__(cls, inst):
        return True


assert isinstance(1, Plain) is False


# --- the hook resolves on the type of the second argument, so a non-type
# --- carrying one is honored before the classinfo check ---
class Carrier:
    def __instancecheck__(self, inst):
        return True


assert isinstance(1, Carrier()) is True


# --- a hook inherited from a metaclass parent ---
class BaseMeta(type):
    def __instancecheck__(cls, inst):
        return isinstance(inst, int)


class DerivedMeta(BaseMeta):
    pass


class CheckDerived(metaclass=DerivedMeta):
    pass


assert isinstance(3, CheckDerived) is True
assert isinstance("s", CheckDerived) is False


# --- the exact-type match still short-circuits the hook ---
class AlwaysFalse(type):
    def __instancecheck__(cls, inst):
        return False


class CheckAlwaysFalse(metaclass=AlwaysFalse):
    pass


assert isinstance(CheckAlwaysFalse(), CheckAlwaysFalse) is True


# --- the plain path is untouched for classes whose type is type ---
class PlainBase:
    pass


class PlainSub(PlainBase):
    pass


assert isinstance(PlainSub(), PlainBase) is True
assert isinstance(PlainBase(), PlainSub) is False
assert issubclass(PlainSub, PlainBase) is True
assert issubclass(bool, int) is True
assert isinstance(1, (int, str)) is True
assert isinstance(1, ()) is False


# --- structural errors keep raising TypeError ---
for call in (
    lambda: isinstance(1, 3),
    lambda: issubclass(int, 3),
    lambda: issubclass(3, int),
    lambda: issubclass(str, (int, 3)),
):
    try:
        call()
        raise AssertionError("an invalid classinfo must raise TypeError")
    except TypeError:
        pass

print("ok")
