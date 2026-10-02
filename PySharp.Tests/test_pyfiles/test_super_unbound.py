"""super(type) builds an unbound super; the three read-only members answer on both.

:kind: test
"""

# Regression: super(Derived) and super(Derived, None) were rejected with
# the supercheck TypeError, although super_init_impl folds obj is None
# into an unbound super before any check. The unbound form had no
# __thisclass__/__self__/__self_class__ members at all, super_repr had
# no NULL branch, and an attribute the MRO walk missed raised instead of
# falling through to GenericGetAttr on the super object itself.


class Base:
    def meth(self):
        return "base.meth:" + self.tag

    @classmethod
    def cmeth(cls):
        return "base.cmeth:" + cls.__name__


class Derived(Base):
    def __init__(self):
        self.tag = "T"


def show(label, f):
    try:
        r = f()
        print(label, "=", repr(r))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


# unbound forms
u = super(Derived)
u2 = super(Derived, None)
show("u-repr", lambda: repr(u))
show("u2-repr", lambda: repr(u2))
show("u-thisclass", lambda: u.__thisclass__ is Derived)
show("u-self", lambda: u.__self__)
show("u-selfclass", lambda: u.__self_class__)


# a class-body super stays unbound until something reads it off an
# instance, and then super_descr_get binds that instance
class H(Derived):
    s = super(Derived)


h = H()
show("H-s-repr", lambda: repr(H.s))
show("h-s-repr", lambda: repr(h.s))
show("h-s-meth", lambda: h.s.meth())
show("h-s-cmeth", lambda: h.s.cmeth())


# bound instance mode
d = Derived()
b = super(Derived, d)
show("b-repr", lambda: repr(b))
show("b-thisclass", lambda: b.__thisclass__ is Derived)
show("b-self-is-d", lambda: b.__self__ is d)
show("b-selfclass", lambda: b.__self_class__ is Derived)
show("b-meth", lambda: b.meth())
show("b-cmeth", lambda: b.cmeth())


# bound class mode: the class binds in place of an instance
c = super(Derived, H)
show("c-repr", lambda: repr(c))
show("c-self-is-H", lambda: c.__self__ is H)
show("c-selfclass-is-H", lambda: c.__self_class__ is H)
show("c-cmeth", lambda: c.cmeth())


def c_meth():
    return c.meth()


show("c-meth", c_meth)
show("b-class-is-super", lambda: b.__class__ is super)


def missing():
    return b.no_such_thing


show("b-missing", missing)
