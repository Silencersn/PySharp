"""
Regression: property must raise AttributeError (not a None-callable
TypeError) when assigning to or deleting a read-only/undeletable
property, and property.__doc__ must inherit the getter's docstring when
no explicit doc is given. getter()/setter()/deleter() return copies
(the original property is untouched), an inherited doc follows the
getter while an explicit doc is kept, and __name__ falls back to the
getter's name.
"""

def expect(exc_type, msg, fn):
    try:
        fn()
    except exc_type as e:
        assert str(e) == msg, str(e)
        return
    raise AssertionError("expected " + exc_type.__name__ + ": " + msg)


class P:
    @property
    def y(self):
        return 1

inst = P()
assert P().y == 1
expect(AttributeError, "property 'y' of 'P' object has no setter",
       lambda: setattr(inst, 'y', 1))
expect(AttributeError, "property 'y' of 'P' object has no deleter",
       lambda: delattr(inst, 'y'))


class WD:
    @property
    def w(self):
        return 1

    @w.setter
    def w(self, value):
        pass

obj = WD()
obj.w = 5
assert obj.w == 1


# __doc__ inheritance
class C:
    @property
    def p(self):
        "the doc"
        return 1

assert C.p.__doc__ == 'the doc'


def g():
    "getter doc here"
    return 1

assert property(g).__doc__ == 'getter doc here'
assert property(g, doc='explicit').__doc__ == 'explicit'
assert property().__doc__ is None

# __name__ falls back to the getter's name
assert C.p.__name__ == 'p'
assert property(g).__name__ == 'g'
try:
    property(g).doc
except AttributeError as e:
    assert str(e) == "'property' object has no attribute 'doc'", str(e)
else:
    raise AssertionError("expected AttributeError")


# getter/setter/deleter return copies; the source property is untouched
base = property(g)
derived = base.setter(lambda s, v: None)
assert derived is not base
assert base.fset is None
assert derived.fset is not None
assert derived.__doc__ == 'getter doc here'

# an inherited doc follows a new getter; an explicit doc is kept
def f1():
    "f1 doc"
    return 1

def f2():
    return 2

assert property(g).getter(f1).__doc__ == 'f1 doc'
assert property(f2).getter(g).__doc__ == 'getter doc here'
assert property(g, doc='x').getter(f1).__doc__ == 'x'

# late class attribute: the message shows the getter's name
class L:
    pass

L.p = property(lambda s: 1)
expect(AttributeError, "property '<lambda>' of 'L' object has no setter",
       lambda: setattr(L(), 'p', 1))

print("test_property_regression passed")

# __set_name__ records the class-attribute name: it wins over the
# getter's function name once the property enters a class body
class Renamed:
    p = property(g)

assert Renamed.p.__name__ == 'p'
expect(AttributeError, "property 'p' of 'Renamed' object has no setter",
       lambda: setattr(Renamed(), 'p', 1))
