"""
static (non-heap) types refuse attribute set/delete.

CPython's type.__setattr__/__delattr__ rejects any attribute write on a
type without Py_TPFLAGS_HEAPTYPE with "cannot set 'X' attribute of
immutable type 'Y'" — the delete path reports "cannot set" too, and the
check fires before existence/descriptor handling. Runtime-created
classes (heap types) keep full set/del support, custom metaclasses stay
assignable, and function instances still accept attributes even though
the function type object itself is sealed.

CPython 3.14 reference (Objects/typeobject.c type_setattro).

:kind: test
"""

_TYPE_ERR = "TypeError"


def expect_te(label, fn, message):
    try:
        fn()
        raise AssertionError(label + ": expected TypeError, got success")
    except TypeError as e:
        assert str(e) == message, label + ": " + str(e)


def expect_ae(label, fn, message):
    try:
        fn()
        raise AssertionError(label + ": expected AttributeError, got success")
    except AttributeError as e:
        assert str(e) == message, label + ": " + str(e)


# --- set on built-in types is refused with the exact CPython message ---
expect_te("set str", lambda: setattr(str, "upper", 5),
          "cannot set 'upper' attribute of immutable type 'str'")
expect_te("set str missing", lambda: setattr(str, "nope1", 1),
          "cannot set 'nope1' attribute of immutable type 'str'")
expect_te("set object", lambda: setattr(object, "x", 1),
          "cannot set 'x' attribute of immutable type 'object'")
expect_te("set type", lambda: setattr(type, "x", 1),
          "cannot set 'x' attribute of immutable type 'type'")
expect_te("set int", lambda: setattr(int, "x", 1),
          "cannot set 'x' attribute of immutable type 'int'")
expect_te("set bool", lambda: setattr(bool, "x", 1),
          "cannot set 'x' attribute of immutable type 'bool'")
expect_te("set dict", lambda: setattr(dict, "x", 1),
          "cannot set 'x' attribute of immutable type 'dict'")
expect_te("set BaseException", lambda: setattr(BaseException, "x", 1),
          "cannot set 'x' attribute of immutable type 'BaseException'")
expect_te("set NoneType", lambda: setattr(type(None), "x", 1),
          "cannot set 'x' attribute of immutable type 'NoneType'")

# iterator-family names: guard message, name compared loosely
for label, target in [("set dict_keys", type({}.keys())), ("set list_iterator", type(iter([])))]:
    try:
        setattr(target, "x", 1)
        raise AssertionError(label + ": expected TypeError, got success")
    except TypeError as e:
        assert str(e).startswith("cannot set 'x' attribute of immutable type '"), label + ": " + str(e)

# --- delete on built-in types reports "cannot set" too ---
expect_te("del str", lambda: delattr(str, "upper"),
          "cannot set 'upper' attribute of immutable type 'str'")
expect_te("del str missing", lambda: delattr(str, "nope2"),
          "cannot set 'nope2' attribute of immutable type 'str'")

# the pollution did not happen
assert "ab".upper() == "AB"

# --- explicit type.__setattr__/__delattr__ hits the same gate ---
expect_te("type.__setattr__", lambda: type.__setattr__(str, "x", 1),
          "cannot set 'x' attribute of immutable type 'str'")
expect_te("type.__delattr__", lambda: type.__delattr__(str, "upper"),
          "cannot set 'upper' attribute of immutable type 'str'")

# --- non-str names keep the string check ---
expect_te("setattr non-str", lambda: setattr(str, 1, 2),
          "attribute name must be string, not 'int'")

# --- function type object sealed, function instances still open ---
def fn():
    return 1


FT = type(fn)
expect_te("set function type", lambda: setattr(FT, "x", 1),
          "cannot set 'x' attribute of immutable type 'function'")
fn.x = 5
assert fn.x == 5
fn.__code__  # still accessible

# --- heap types keep full set/del support ---
class A:
    pass


A.y = 5
assert A.y == 5
delattr(A, "y")
old_name = A.__name__
A.__name__ = "B2"
assert A.__name__ == "B2"
A.__name__ = old_name


class B(int):
    def triple(self):
        return self * 3


B.foo = 1
assert B.foo == 1 and B(2).triple() == 6
B.quad = lambda self: self * 4
assert B(2).quad() == 8


class M(type):
    pass


M.mc = 1
assert M.mc == 1

# deleting a missing attribute on a heap type uses the type-object message
expect_ae("del heap missing", lambda: delattr(A, "neverthere"),
          "type object 'A' has no attribute 'neverthere'")

print("type attr guard passed")
