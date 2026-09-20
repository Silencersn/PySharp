"""Regression: function.__defaults__ is writable (Objects/funcobject.c
func_get_defaults/func_set_defaults). Assigning a tuple replaces the
defaults wholesale, the assigned tuple reads back as the same object, a
tuple subclass is accepted (PyTuple_Check), a non-tuple is a TypeError,
and None or a deletion clears the defaults. The call binder reads the
live tuple (Python/ceval.c initialize_locals), which is what makes a
rewrite reach the next call.
"""


def expect_type_error(label, fn, message):
    try:
        fn()
    except TypeError as error:
        assert str(error) == message, (label, str(error), message)
        return
    raise AssertionError(label + ": no TypeError")


def assign_defaults(target, value):
    target.__defaults__ = value


# a rewrite reaches the next call
def h(a=1, b=2):
    return (a, b)


assert h.__defaults__ == (1, 2)
h.__defaults__ = (9, 9)
assert h() == (9, 9)
assert h(1) == (1, 9)
assert h(b=9) == (9, 9)

# the assigned tuple is the object handed back
t = (7, 8)
h.__defaults__ = t
assert h.__defaults__ is t
assert h.__defaults__ is h.__defaults__


# an untouched function hands back the same tuple every read
def plain(a=1):
    return a


assert plain.__defaults__ == (1,)
assert plain.__defaults__ is plain.__defaults__


def nod(a, b):
    return (a, b)


assert nod.__defaults__ is None
nod.__defaults__ = (1, 2)
assert nod() == (1, 2)


# None clears the defaults, and so does a deletion
def cleared(a=1, b=2):
    return (a, b)


cleared.__defaults__ = None
assert cleared.__defaults__ is None
expect_type_error("cleared-call", lambda: cleared(),
                  "cleared() missing 2 required positional arguments: 'a' and 'b'")
cleared.__defaults__ = (3, 4)
assert cleared() == (3, 4)
del cleared.__defaults__
assert cleared.__defaults__ is None
expect_type_error("deleted-call", lambda: cleared(),
                  "cleared() missing 2 required positional arguments: 'a' and 'b'")


# an empty tuple is not None
def empty(a=1):
    return a


empty.__defaults__ = ()
assert empty.__defaults__ == ()
assert empty.__defaults__ is not None
expect_type_error("empty-call", lambda: empty(),
                  "empty() missing 1 required positional argument: 'a'")


# only a tuple is accepted, and a rejected value leaves the defaults alone
for bad in ([1, 2], "ab", 5, {"a": 1}):
    expect_type_error("non-tuple", lambda bad=bad: assign_defaults(h, bad),
                      "__defaults__ must be set to a tuple object")
assert h.__defaults__ is t


# a tuple subclass goes through (PyTuple_Check)
class MyTuple(tuple):
    pass


h.__defaults__ = MyTuple((5, 6))
assert h() == (5, 6)
assert h(b=9) == (5, 9)


# a shorter tuple moves the parameters that lack a value
def short(a=1, b=2, c=3):
    return (a, b, c)


short.__defaults__ = (9,)
expect_type_error("short-call", lambda: short(),
                  "short() missing 2 required positional arguments: 'a' and 'b'")
assert short(1, 2) == (1, 2, 9)


# the arity message follows the live count
def arity(a, b):
    return (a, b)


arity.__defaults__ = None
expect_type_error("arity-none", lambda: arity(1, 2, 3),
                  "arity() takes 2 positional arguments but 3 were given")
arity.__defaults__ = (1,)
expect_type_error("arity-one", lambda: arity(1, 2, 3),
                  "arity() takes from 1 to 2 positional arguments but 3 were given")


# more defaults than parameters: the trailing ones apply, because the fill
# offset is co_argcount - len(func_defaults) on both sides
def over(a, b):
    return (a, b)


over.__defaults__ = (1, 2, 3)
assert over() == (2, 3)
assert over(5) == (5, 3)
expect_type_error("over-arity", lambda: over(1, 2, 3),
                  "over() takes from -1 to 2 positional arguments but 3 were given")


# positional-only parameters are covered too
def posonly(a=1, /, b=2):
    return (a, b)


posonly.__defaults__ = (8, 9)
assert posonly() == (8, 9)
assert posonly(0) == (0, 9)
posonly.__defaults__ = (8,)
expect_type_error("posonly-call", lambda: posonly(),
                  "posonly() missing 1 required positional argument: 'a'")
assert posonly(1) == (1, 8)


# *args and **kwargs leave the binding untouched
def variadic(a, *rest, b=1, **kw):
    return (a, rest, b, kw)


variadic.__defaults__ = None
assert variadic(1) == (1, (), 1, {})


# a method, a lambda and a nested function share the same descriptor
class Holder:
    def method(self, x=1):
        return x


Holder.method.__defaults__ = (5,)
assert Holder().method() == 5
assert Holder.method(Holder()) == 5

lam = lambda x=1: x
lam.__defaults__ = (3,)
assert lam() == 3
assert lam(0) == 0


def outer():
    def inner(z=1):
        return z

    inner.__defaults__ = (4,)
    return inner


assert outer()() == 4


def called(a=1):
    return a


called.__defaults__ = (11,)
assert called() == 11
assert called.__call__() == 11


# every function built from a code object keeps its own defaults
def factory():
    def inner(a=1):
        return a

    return inner


first = factory()
second = factory()
first.__defaults__ = (5,)
assert first() == 5
assert second() == 1

funcs = []
for _ in range(2):
    def looped(a=1):
        return a

    funcs.append(looped)

funcs[0].__defaults__ = (7,)
assert funcs[0]() == 7
assert funcs[1]() == 1

print("ok")
