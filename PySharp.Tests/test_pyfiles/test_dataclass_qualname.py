"""Synthesized dataclass methods carry a class-qualified __qualname__, so binding errors name P.__init__ and friends.

:kind: test
"""

# Regression: the exec'ed method sources name their functions bare, but
# CPython stamps class-qualified names on every synthesized method; the
# prefix is the class's __qualname__, so nesting and local classes keep
# their Outer.Inner / make.<locals>.Local segments, a subclass reads its
# own name, and a user-defined method is left untouched.


from dataclasses import dataclass


def show(label, f):
    try:
        print(label, "=", repr(f()))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


@dataclass
class P:
    x: int
    y: int = 0


show("qualname_init", lambda: P.__init__.__qualname__)
show("qualname_repr", lambda: P.__repr__.__qualname__)
show("qualname_eq", lambda: P.__eq__.__qualname__)
show("missing_arg", lambda: P())
show("repr_extra", lambda: P(1).__repr__(1, 2))
show("eq_extra", lambda: P(1).__eq__(P(1), P(1)))


@dataclass
class Outer:
    @dataclass
    class Inner:
        a: int


show("nested_qualname", lambda: Outer.Inner.__init__.__qualname__)
show("nested_missing", lambda: Outer.Inner())


def make():
    @dataclass
    class Local:
        a: int
    return Local


L = make()
show("local_qualname", lambda: L.__init__.__qualname__)
show("local_missing", lambda: L())


@dataclass
class Base:
    a: int


@dataclass
class Sub(Base):
    b: int = 0


show("sub_qualname", lambda: Sub.__init__.__qualname__)
show("takes_msg", lambda: P(1, 2, 3, 4))
show("unexpected_kw", lambda: P(1, zz=2))
show("missing_two", lambda: P.__init__(1))


class Custom:
    def __repr__(self):
        return "C"


show("custom_repr_qualname", lambda: Custom.__repr__.__qualname__)
