"""
Regression: excess constructor arguments on classes that define neither
__new__ nor __init__ (the object defaults) must raise TypeError like
CPython object_new/object_init, not be silently swallowed.

PySharp used to guard with `ReferenceEquals(cls, object)`, so only exact
object() complained; any subclass (even `class C: pass`) accepted and
ignored extra positional/keyword arguments and still created the instance.

CPython 3.14 rules (typeobject.c):
  object.__new__ with excess args errors with "<Name>() takes no arguments"
  unless the class overrides __new__ (-> "takes exactly one argument") or
  overrides __init__ (args are forwarded to it);
  object.__init__ mirrors that: excess args error unless a custom __new__
  consumed them;
  assigning the inherited object defaults (__new__ / __init__) back onto a
  class is a no-op for slot resolution.
"""

def expect_type_error(fn, *args, **kwargs):
    try:
        fn(*args, **kwargs)
    except TypeError as e:
        return e
    raise AssertionError(f"TypeError not raised: {fn}")


class C:
    pass


# --- red cases: excess args on a plain class -----------------------------

e = expect_type_error(C, 1)
assert e.args == ("C() takes no arguments",)
e = expect_type_error(C, 1, 2)
assert e.args == ("C() takes no arguments",)
e = expect_type_error(C, x=1)
assert e.args == ("C() takes no arguments",)
e = expect_type_error(C, 1, x=2)
assert e.args == ("C() takes no arguments",)

# same through the direct object.__new__ builtin and the inherited attribute
e = expect_type_error(object.__new__, C, 1)
assert e.args == ("C() takes no arguments",)
e = expect_type_error(C.__new__, C, 1)
assert e.args == ("C() takes no arguments",)

# exact object() face uses the same message in CPython 3.14
e = expect_type_error(object, 1)
assert e.args == ("object() takes no arguments",)
e = expect_type_error(object, x=1)
assert e.args == ("object() takes no arguments",)
e = expect_type_error(object.__new__, object, 1)
assert e.args == ("object() takes no arguments",)

# subclass chains and dynamically/function-locally created classes report
# their own name
class S(C):
    pass

class G(C):
    pass

class H(G):
    pass

for cls in (S, H, type("T", (), {})):
    e = expect_type_error(cls, 2)
    assert e.args == (f"{cls.__name__}() takes no arguments",)

def make_local():
    class Local:
        pass
    return expect_type_error(Local, 1).args

assert make_local() == ("Local() takes no arguments",)


# --- custom __init__ / __new__ combinations ------------------------------

class D:
    def __init__(self):
        pass

# default __new__ + custom __init__: object.__new__ tolerates the args
# (the custom __init__ is there to receive them); the constructor call
# itself still fails in the custom __init__ binding (message not asserted)
d = object.__new__(D, 1)
assert type(d) is D
assert expect_type_error(D, 1).__class__ is TypeError

class E:
    def __new__(cls, *args):
        return super().__new__(cls)

# custom __new__ + default __init__: the constructor tolerates excess args
assert type(E(1)) is E

# but explicitly calling object.__new__ on a class that overrides __new__
# complains about the extra arguments
e = expect_type_error(object.__new__, E, 1)
assert e.args == ("object.__new__() takes exactly one argument (the type to instantiate)",)


# --- object.__init__ excess-argument faces --------------------------------

c0 = C()
d0 = D()
e0 = E()

e = expect_type_error(object.__init__, c0, 1)
assert e.args == ("C.__init__() takes exactly one argument (the instance to initialize)",)
e = expect_type_error(object.__init__, c0, x=1)
assert e.args == ("C.__init__() takes exactly one argument (the instance to initialize)",)
e = expect_type_error(C.__init__, c0, 1)
assert e.args == ("C.__init__() takes exactly one argument (the instance to initialize)",)

# instance whose type overrides __init__
e = expect_type_error(object.__init__, d0, 1)
assert e.args == ("object.__init__() takes exactly one argument (the instance to initialize)",)

# instance whose type overrides __new__: tolerated (the __new__ signature
# governs), and the no-argument form is always fine
assert object.__init__(e0, 1) is None
assert object.__init__(c0) is None

# cooperative super().__init__ chain reaching object.__init__ with args
class Coop:
    def __init__(self, *a):
        super().__init__(*a)

assert type(Coop()) is Coop
e = expect_type_error(Coop, 1)
assert e.args == ("object.__init__() takes exactly one argument (the instance to initialize)",)


# --- exception family: args consumed by __new__, object.__init__ errors --

v = ValueError("x")
assert v.args == ("x",)
e = expect_type_error(object.__init__, v, 1)
assert e.args == ("object.__init__() takes exactly one argument (the instance to initialize)",)
assert object.__init__(v) is None

class MyErr(ValueError):
    pass

assert MyErr("boom").args == ("boom",)

class MyExit(SystemExit):
    def __init__(self, *a):
        super().__init__(*a)

assert MyExit("z").code == "z"

# exceptions no longer inherit object's __init__ identity
assert Exception.__init__ is not object.__init__
assert SystemExit.__init__ is not object.__init__


# --- type.__init__ accepts the cooperative 1/3-argument forms -------------

assert type.__init__(int, "x", (), {}) is None
assert type.__init__(int, "x", (), {}, kw=1) is None  # kwds ignored for 3 args
e = expect_type_error(type.__init__, int, "a", "b")
assert e.args == ("type.__init__() takes 1 or 3 arguments",)
e = expect_type_error(type.__init__, int)
assert e.args == ("type.__init__() takes 1 or 3 arguments",)
e = expect_type_error(type.__init__, int, x=1)
assert e.args == ("type.__init__() takes 1 or 3 arguments",)
assert type.__init__ is not object.__init__

# metaclass __init__ chains through super().__init__(name, bases, attrs)
class Meta(type):
    def __init__(cls, name, bases, attrs, **kwargs):
        super().__init__(name, bases, attrs)
        cls.tagged = kwargs.get("tag", "none")

NS = Meta("MCls", (), {})
assert NS.tagged == "none"
assert type(NS) is Meta

# class keywords reach the metaclass __init__ once __init_subclass__
# accepts them (the object default rejects keywords)
class KWMetaBase:
    def __init_subclass__(cls, **kwargs):
        pass

NS2 = Meta("MCls2", (KWMetaBase,), {}, tag="custom")
assert NS2.tagged == "custom"
assert type(NS2) is Meta

class Meta2(type):
    pass

assert type(Meta2("S2", (), {})) is Meta2


# --- re-assigning the inherited object defaults is a no-op ----------------

class K:
    __new__ = object.__new__

e = expect_type_error(K, 1)
assert e.args == ("K() takes no arguments",)

class K2:
    __init__ = object.__init__

e = expect_type_error(K2, 1)
assert e.args == ("K2() takes no arguments",)

# setattr form as well
class K3:
    pass

K3.__init__ = object.__init__
e = expect_type_error(K3, 1)
assert e.args == ("K3() takes no arguments",)


# --- guards: unchanged behavior -------------------------------------------

assert isinstance(object.__new__(C), C)
assert isinstance(object.__new__(object), object)
assert isinstance(object(), object)
p = C()
p.attr = 42
assert p.attr == 42
assert int(5) == 5
assert type(EX := ValueError("m")) is ValueError

print("test_object_new_excess_args_regression passed")
