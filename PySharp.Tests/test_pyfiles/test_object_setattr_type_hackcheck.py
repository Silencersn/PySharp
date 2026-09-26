"""
object.__setattr__/object.__delattr__ refuse every
type-object target with "can't apply this __setattr__/__delattr__ to
<N> object", like CPython's hackcheck (Objects/typeobject.c
wrap_setattr/wrap_delattr). Previously they silently wrote into the
class dict or fell into the immutable-type error family.

The message names the target's own type: plain classes (metaclass
`type`) show "type object", heap-metaclass instances show the
metaclass name ("Meta object").

CPython keeps object.__setattr__(instance/module, ...) as the generic
bypassing setattr, and type.__setattr__/__delattr__ as the separate
type_setattro wrappers: type targets pass, non-type targets get
"descriptor '<name>' requires a 'type' object but received a '<T>'".
The plain `cls.x = v` paths and the existing guards (immutable type,
__doc__ deletion, __class__) are unaffected.

:kind: test
"""

import sys


def err(fn):
    try:
        return ('ok', fn())
    except Exception as e:
        return (type(e).__name__, str(e))


class P1:
    pass


# type targets are refused before anything else, regardless of the
# attribute name or whether the type is mutable
assert err(lambda: object.__setattr__(P1, 'zzz', 1)) == \
    ('TypeError', "can't apply this __setattr__ to type object")
assert err(lambda: object.__setattr__(P1, '__doc__', 'd')) == \
    ('TypeError', "can't apply this __setattr__ to type object")
assert 'zzz' not in vars(P1) and P1.__doc__ != 'd'

assert err(lambda: object.__delattr__(P1, '__doc__')) == \
    ('TypeError', "can't apply this __delattr__ to type object")
assert err(lambda: object.__delattr__(P1, '__class__')) == \
    ('TypeError', "can't apply this __delattr__ to type object")
assert err(lambda: object.__setattr__(int, 'x', 1)) == \
    ('TypeError', "can't apply this __setattr__ to type object")
assert err(lambda: object.__delattr__(int, '__doc__')) == \
    ('TypeError', "can't apply this __delattr__ to type object")
assert err(lambda: object.__setattr__(P1, '__class__', P1)) == \
    ('TypeError', "can't apply this __setattr__ to type object")
assert err(lambda: object.__setattr__(type, 'zzz', 3)) == \
    ('TypeError', "can't apply this __setattr__ to type object")


# the message names the target's metaclass for heap-metaclass instances
class Meta(type):
    pass


class WM(metaclass=Meta):
    pass


assert err(lambda: object.__setattr__(Meta, 'zzz', 2)) == \
    ('TypeError', "can't apply this __setattr__ to type object")
assert err(lambda: object.__setattr__(WM, 'zzz', 2)) == \
    ('TypeError', "can't apply this __setattr__ to Meta object")
assert err(lambda: object.__delattr__(WM, 'zzz')) == \
    ('TypeError', "can't apply this __delattr__ to Meta object")


class MetaSet(type):
    def __setattr__(self, name, value):
        raise RuntimeError("meta setattr called")


class WM2(metaclass=MetaSet):
    pass


assert err(lambda: object.__setattr__(WM2, 'z', 1)) == \
    ('TypeError', "can't apply this __setattr__ to MetaSet object")


# instance and module targets keep the bypassing generic setattr
class Custom:
    def __setattr__(self, name, value):
        raise RuntimeError("custom called")


inst = Custom()
assert object.__setattr__(inst, 'x', 1) is None and inst.x == 1
assert object.__setattr__(sys, 'x', 1) is None
assert object.__delattr__(sys, 'x') is None

# instance-bound object.__setattr__ keeps working
i = P1()
i.__setattr__('y', 5)
assert i.y == 5


# type.__setattr__/__delattr__ are the type_setattro wrappers: type
# targets pass, non-type targets are rejected
class Q:
    pass


assert type.__setattr__(Q, 'zzz', 1) is None and Q.zzz == 1
assert type.__delattr__(Q, 'zzz') is None
assert err(lambda: type.__setattr__(int, 'x', 1)) == \
    ('TypeError', "cannot set 'x' attribute of immutable type 'int'")
assert err(lambda: type.__setattr__(Q(), 'x', 1)) == \
    ('TypeError', "descriptor '__setattr__' requires a 'type' object but received a 'Q'")
assert err(lambda: type.__setattr__(sys, 'x', 1)) == \
    ('TypeError', "descriptor '__setattr__' requires a 'type' object but received a 'module'")
assert err(lambda: type.__delattr__(Q(), 'x')) == \
    ('TypeError', "descriptor '__delattr__' requires a 'type' object but received a 'Q'")


# plain cls paths are untouched
class R:
    pass


assert err(lambda: setattr(R, 'zzz', 1)) == ('ok', None) and R.zzz == 1
assert err(lambda: delattr(R, 'zzz')) == ('ok', None)
R.__doc__ = 'd'
assert R.__doc__ == 'd'
assert err(lambda: delattr(R, '__doc__')) == \
    ('TypeError', "cannot delete '__doc__' attribute of immutable type 'R'")
assert err(lambda: setattr(int, 'x', 1)) == \
    ('TypeError', "cannot set 'x' attribute of immutable type 'int'")

print("test_object_setattr_type_hackcheck passed")
