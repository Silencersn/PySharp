"""
Regression: redundant bases already covered by another base's MRO must not
raise a layout conflict.

CPython's best_base (Objects/typeobject.c) compares solid bases with the
subtype relation: a later base whose layout is the same or an ancestor of
the current winner's layout (e.g. a redundant object after a derived
layout owner) keeps the winner, so class K(B, object) is created with
__bases__ unchanged. Only unrelated layouts raise "multiple bases have
instance lay-out conflict". An unlinearizable base order reaches the MRO
merge, whose set_mro_error reports the blocked heads:
"Cannot create a consistent method resolution order (MRO) for bases X, Y".
"""

# --- the reported cases: a redundant object after a derived layout ---
class B:
    pass

class K1(B, object):
    pass

assert len(K1.__bases__) == 2, K1.__bases__
assert [c.__name__ for c in K1.__mro__] == ["K1", "B", "object"], K1.__mro__

K2 = type("K2", (B, object), {})
assert K2.__name__ == "K2"
assert [c.__name__ for c in K2.__mro__] == ["K2", "B", "object"], K2.__mro__

# --- controls: deep parents and diamonds in the user-class chain ---
class A:
    pass

class B2(A):
    pass

class C(B2):
    pass

class K3(B2, A):
    pass

assert [c.__name__ for c in K3.__mro__] == ["K3", "B2", "A", "object"], K3.__mro__

class K4(C, B2):
    pass

assert [c.__name__ for c in K4.__mro__] == ["K4", "C", "B2", "A", "object"], K4.__mro__

# --- duplicate bases are still refused (equal-value duplicates) ---
try:
    class D1(B, B):
        pass
    raise AssertionError("duplicate bases must be refused")
except TypeError as e:
    assert str(e) == "duplicate base class B", str(e)

# --- unlinearizable orders fail in the MRO step with the bases tail ---
try:
    class M1(object, B):
        pass
    raise AssertionError("(object, B) must not linearize")
except TypeError as e:
    assert str(e) == "Cannot create a consistent method resolution order (MRO) for bases object, B", str(e)

# --- the same rules for built-in layouts ---
class D(dict):
    pass

class K5(D, dict):
    pass

assert [c.__name__ for c in K5.__mro__] == ["K5", "D", "dict", "object"], K5.__mro__

try:
    class M2(dict, D):
        pass
    raise AssertionError("(dict, D) must not linearize")
except TypeError as e:
    assert str(e) == "Cannot create a consistent method resolution order (MRO) for bases dict, D", str(e)

try:
    class L1(int, D):
        pass
    raise AssertionError("unrelated layouts must conflict")
except TypeError as e:
    assert str(e) == "multiple bases have instance lay-out conflict", str(e)

class K6(int, object):
    pass

assert [c.__name__ for c in K6.__mro__] == ["K6", "int", "object"], K6.__mro__

try:
    class M3(object, int):
        pass
    raise AssertionError("(object, int) must not linearize")
except TypeError as e:
    assert str(e) == "Cannot create a consistent method resolution order (MRO) for bases object, int", str(e)

print("redundant base layout regression passed")
