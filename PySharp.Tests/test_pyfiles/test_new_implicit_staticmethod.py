"""
a plain-function __new__ in a class body is implicitly a
staticmethod.

CPython's type_new runs type_new_staticmethod over the class namespace
right beside the classmethod wrap of __init_subclass__ / __class_getitem__
(Objects/typeobject.c), so super().__new__(cls, ...) resolves the function
unbound and passes the class exactly once. Without the wrap the function
descriptor re-bound it to the class: a metaclass chain died with an arity
TypeError, and a varargs __new__ forwarded every argument one position off
without any error at all.

CPython 3.14 reference (Objects/typeobject.c type_new_staticmethod and
type_new_classmethod, called from type_new_set_attrs).

:kind: test
"""

# --- the reported shape: two user metaclasses delegating through super() ---
calls = []


class M1(type):
    def __new__(mcls, name, bases, ns):
        calls.append("M1:" + name)
        return super().__new__(mcls, name, bases, ns)


class M2(M1):
    def __new__(mcls, name, bases, ns):
        calls.append("M2:" + name)
        return super().__new__(mcls, name, bases, ns)


class Meta(metaclass=M2):
    pass

assert calls == ["M2:Meta", "M1:Meta"], calls
assert type(Meta) is M2
assert [c.__name__ for c in Meta.__mro__] == ["Meta", "object"]


# --- three levels, plus a metaclass that only inherits __new__ ---
chain = []


class C1(type):
    def __new__(mcls, name, bases, ns):
        chain.append("C1")
        return super().__new__(mcls, name, bases, ns)


class C2(C1):
    def __new__(mcls, name, bases, ns):
        chain.append("C2")
        return super().__new__(mcls, name, bases, ns)


class C3(C2):
    def __new__(mcls, name, bases, ns):
        chain.append("C3")
        return super().__new__(mcls, name, bases, ns)


class Deep(metaclass=C3):
    pass

assert chain == ["C3", "C2", "C1"], chain


class Skipping(C1):
    pass


class ViaSkip(metaclass=Skipping):
    pass

assert chain == ["C3", "C2", "C1", "C1"], chain


# --- ordinary classes take the same path ---
class Base:
    def __new__(cls, x):
        return super().__new__(cls)


class Middle(Base):
    def __new__(cls, x):
        return super().__new__(cls, x)


class Leaf(Middle):
    pass

assert type(Leaf(5)) is Leaf


class Base0:
    def __new__(cls):
        return super().__new__(cls)


class Middle0(Base0):
    def __new__(cls):
        return super().__new__(cls)


class Leaf0(Middle0):
    pass

assert type(Leaf0()) is Leaf0


# --- varargs arrive once, not shifted by one ---
seen = []


class V1:
    def __new__(cls, *args, **kwargs):
        seen.append(("V1", args, tuple(sorted(kwargs.items()))))
        return super().__new__(cls)


class V2(V1):
    def __new__(cls, *args, **kwargs):
        seen.append(("V2", args, tuple(sorted(kwargs.items()))))
        return super().__new__(cls, *args, **kwargs)


class V3(V2):
    pass

V3(1, 2, k=3)
assert seen == [
    ("V2", (1, 2), (("k", 3),)),
    ("V1", (1, 2), (("k", 3),)),
], seen


# --- the attribute is a staticmethod whose lookup ignores the class ---
class Parent(type):
    def __new__(mcls, name, bases, ns):
        return super().__new__(mcls, name, bases, ns)


class Child(Parent):
    pass


class GrandChild(metaclass=Child):
    pass

assert type(Parent.__dict__["__new__"]).__name__ == "staticmethod"
assert type(Parent.__new__).__name__ == "function"
# the inherited entry is a staticmethod, so super() yields the plain function
assert type(super(Child, GrandChild).__new__).__name__ == "function"


# --- type(name, bases, ns) takes the same path ---
def dynamic_new(cls, x):
    return object.__new__(cls)


Dynamic = type("Dynamic", (), {"__new__": dynamic_new})
assert type(Dynamic.__dict__["__new__"]).__name__ == "staticmethod"


class DynamicSub(Dynamic):
    def __new__(cls, x):
        return super().__new__(cls, x)


assert type(DynamicSub(1)) is DynamicSub


class DynamicPlain(Dynamic):
    pass

assert type(DynamicPlain(2)) is DynamicPlain


# --- the neighbouring classmethod wraps are unchanged ---
subclasses = []


class HookBase:
    def __init_subclass__(cls, **kwargs):
        subclasses.append((cls.__name__, tuple(sorted(kwargs.items()))))


class HookSub(HookBase, tag="x"):
    pass

assert subclasses == [("HookSub", (("tag", "x"),))], subclasses


class Container:
    def __class_getitem__(cls, item):
        return (cls.__name__, item)


alias = Container[int]
assert alias[0] == "Container" and alias[1] is int

print("ok")
