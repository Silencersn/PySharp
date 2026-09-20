"""
Regression: class-object attribute access falls back to the metaclass MRO.

CPython's _Py_type_getattro_impl (Objects/typeobject.c) looks the name up on
the metatype, then on the type's own MRO, then calls the metatype entry if it
is a descriptor, and finally returns the metatype entry even when it is an
ordinary attribute. The last step was missing: metaclass data was invisible
through the class while the class's dir() still listed it, so getattr raised
AttributeError, hasattr answered False and getattr(x, name, default) handed
back the default.

CPython 3.14 reference (Objects/typeobject.c _Py_type_getattro_impl).
"""


# --- the reported shape: a metaclass attribute two levels up ---
class M1(type):
    marker = "M1"
    table = [1, 2]


class M2(M1):
    pass


class A(metaclass=M2):
    pass


assert A.marker == "M1"
assert A.table == [1, 2]
assert type(A).marker == "M1"
assert hasattr(A, "marker")
assert getattr(A, "marker", "default") == "M1"


# --- plain values on the direct metaclass, opaque objects included ---
class Direct(type):
    p = "meta-p"
    opaque = object()


class B(metaclass=Direct):
    pass


assert B.p == "meta-p"
assert B.opaque is Direct.opaque


# --- precedence is unchanged ---
class Meta(type):
    x = "meta-x"

    @property
    def prop(cls):
        return "prop:" + cls.__name__


class C(metaclass=Meta):
    x = "cls-x"
    prop = "cls-prop"


# the class dict beats a metaclass non-data entry ...
assert C.x == "cls-x"
# ... while a metaclass data descriptor beats the class dict
assert C.prop == "prop:C"


class Desc:
    def __get__(self, obj, objtype=None):
        return (obj.__name__, objtype.__name__)


class Describing(type):
    desc = Desc()

    def helper(cls):
        return "helper:" + cls.__name__


class D(metaclass=Describing):
    pass


assert D.desc == ("D", "Describing")
assert D.helper() == "helper:D"


# --- a metaclass __getattr__ stays the last resort ---
class Fallback(type):
    def __getattr__(cls, name):
        return "fallback:" + name


class E(metaclass=Fallback):
    own = "own"


assert E.own == "own"
assert E.anything == "fallback:anything"


# --- instances still see nothing from the metaclass ---
class F(metaclass=M1):
    pass


try:
    F().marker
    raise AssertionError("instances must not see metaclass attributes")
except AttributeError as e:
    assert str(e) == "'F' object has no attribute 'marker'", str(e)

# a genuinely missing name still raises
try:
    getattr(F, "definitely_missing")
    raise AssertionError("a missing attribute must raise AttributeError")
except AttributeError as e:
    assert str(e) == "type object 'F' has no attribute 'definitely_missing'", str(e)

print("ok")
