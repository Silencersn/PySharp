"""Verifies that __class__ and __dict__ resolve as getset descriptors on the MRO instead of hardcoded read paths: class-body, base and property shadowing wins first, dir()/vars() list both names, and isinstance()/super() honor a proxy's reported __class__.

:kind: test
"""

# test_class_dict_descriptor: __class__/__dict__ as MRO-driven getset descriptors


def expect(exc_type, func, *args):
    try:
        func(*args)
        return "no exception"
    except exc_type as e:
        return str(e)


# --- read shadowing -------------------------------------------------------

class ClassShadow:
    __class__ = 5

print("body_shadow:", ClassShadow().__class__)

class BaseShadow:
    __class__ = 7

class DerivedShadow(BaseShadow):
    pass

print("base_shadow:", DerivedShadow().__class__)

class PropertyShadow:
    @property
    def __class__(self):
        return str

print("property_shadow:", PropertyShadow().__class__)

class DictBody:
    __dict__ = {"m": 1}

d = DictBody()
print("dict_body_read:", d.__dict__)

class DictProperty:
    @property
    def __dict__(self):
        return {"m": 2}

print("dict_property_read:", DictProperty().__dict__)

# a shadowed __dict__ has no setter: writes and deletes land in the
# instance dict as a plain name, reads pick the instance entry first
d.__dict__ = {"z": 9}
print("dict_body_write:", d.__dict__)
del d.__dict__
print("dict_body_delete:", d.__dict__)

# --- descriptor faces ----------------------------------------------------

print("vars_object_class:", "__class__" in vars(object))
print("int_class_is_int:", (5).__class__ is int)

class Plain:
    def meth(self):
        return 1

print("dir_int_class:", "__class__" in dir(5))
print("dir_inst_class:", "__class__" in dir(Plain()))
print("dir_class_dict:", "__dict__" in dir(Plain))
print("dir_class_class:", "__class__" in dir(Plain))
print("vars_class_dict:", "__dict__" in vars(Plain))
print("dict_descr_type:", type(Plain.__dict__["__dict__"]).__name__)
print("dict_descr_repr:", repr(Plain.__dict__["__dict__"]))
print("class_descr_repr:", repr(object.__dict__["__class__"]))

p = Plain()
p.x = 1
print("inst_dict:", p.__dict__)
holder = {"k": 9}
p.__dict__ = holder
print("dict_replace:", p.k, p.__dict__ is holder)
print("dict_write_non_dict:", expect(TypeError, setattr, p, "__dict__", 42))
print("dict_delete:", expect(None, delattr, p, "__dict__"), p.__dict__)
print("int_dict:", expect(AttributeError, getattr, 5, "__dict__"))
print("class_delete:", expect(TypeError, delattr, 5, "__class__"))
print("class_write_non_type:", expect(TypeError, setattr, p, "__class__", 42))

# --- isinstance proxies ---------------------------------------------------

class Base:
    def hello(self):
        return "base-hello"

class PropProxy:
    @property
    def __class__(self):
        return Base

class AttrProxy:
    __class__ = Base

class SwapProxy:
    def __init__(self):
        self.__class__ = Base

for cls in (PropProxy, AttrProxy, SwapProxy):
    o = cls()
    print("proxy:", cls.__name__, isinstance(o, Base))

class SubBase(Base):
    pass

class SubProxy:
    @property
    def __class__(self):
        return SubBase

print("proxy_tuple:", isinstance(PropProxy(), (int, Base)))
print("proxy_sub:", isinstance(SubProxy(), Base), isinstance(SubProxy(), SubBase))
print("proxy_nontype:", not isinstance(PropProxy, int))
print("proxy_real:", isinstance(Base(), Base))

class NonType:
    @property
    def __class__(self):
        return 42

print("proxy_nontype_override:", isinstance(NonType(), int))

class Raising:
    @property
    def __class__(self):
        raise RuntimeError("boom")

print("proxy_raise:", expect(RuntimeError, isinstance, Raising(), int))

# --- super proxies --------------------------------------------------------

print("super_proxy:", type(super(Base, PropProxy())).__name__)
# the proxy binds, but the search starts after Base in the reported class's
# MRO — Base's own hello stays out of reach
print("super_skips_reported:", expect(AttributeError, lambda: super(Base, PropProxy()).hello))
print("super_attr_proxy:", type(super(Base, AttrProxy())).__name__)

# --- unshadowed faces stay exact -----------------------------------------

print("int_class:", (5).__class__)
print("str_class:", "s".__class__)

print("All class/dict descriptor tests passed")
