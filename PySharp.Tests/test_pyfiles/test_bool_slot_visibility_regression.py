"""
Regression: __bool__ slot visibility must match CPython. Types without a
real bool slot (object, str, bytes, bytearray, list, tuple, dict, set,
frozenset, memoryview, super, type) must not expose __bool__ at all; their
truthiness comes from the dispatch-layer __len__ fallback (CPython
PyObject_IsTrue). Types with a real slot (int, float, bool, complex,
NoneType, NotImplementedType, range) keep __bool__.
"""

no_bool = (object, str, bytes, bytearray, list, tuple, dict, set,
           frozenset, memoryview, super, type)
has_bool = (int, float, bool, complex, type(None), type(NotImplemented), range)

for t in no_bool:
    assert not hasattr(t, "__bool__"), t

for t in has_bool:
    assert hasattr(t, "__bool__"), t

# instances expose no __bool__ either; the direct call is an AttributeError
o = object()
assert not hasattr(o, "__bool__")
try:
    o.__bool__()
except AttributeError:
    pass
else:
    raise AssertionError("object() exposes __bool__")

# truthiness itself is unchanged: length-based via the dispatch fallback
assert bool("") is False and bool("a") is True
assert bool([]) is False and bool([0]) is True
assert bool(()) is False and bool((1,)) is True
assert bool({}) is False and bool({1: 2}) is True
assert bool(set()) is False and bool({1}) is True
assert bool(frozenset()) is False and bool(frozenset({1})) is True
assert bool(b"") is False and bool(b"x") is True
assert bool(bytearray()) is False and bool(bytearray(b"x")) is True
assert bool(memoryview(b"")) is False and bool(memoryview(b"ab")) is True
assert bool(object()) is True
assert bool(range(0)) is False and bool(range(3)) is True
assert not [] and [0]
assert not {} and {1: 1}

# real slots remain directly callable
assert range.__bool__(range(5)) is True
assert range.__bool__(range(0)) is False
assert None.__bool__() is False
assert int.__bool__(0) is False
assert complex.__bool__(complex(0, 1)) is True

# NotImplemented.__bool__ is the hard-error slot
try:
    NotImplemented.__bool__()
except TypeError:
    pass
else:
    raise AssertionError("NotImplemented.__bool__ must raise")

# user classes: no __bool__ unless defined; __len__ drives truthiness
class Empty:
    def __len__(self):
        return 0

assert not hasattr(Empty, "__bool__")
assert bool(Empty()) is False

class WithBool:
    def __bool__(self):
        return False

assert hasattr(WithBool, "__bool__")
assert WithBool().__bool__() is False

print("test_bool_slot_visibility_regression passed")
