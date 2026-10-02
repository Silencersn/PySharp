"""Heap-type __bases__ assignment recomputes the MRO of the whole subtree.

:kind: test
"""

# Regression: the write fell through to the bare "readonly attribute"
# sentence and was dropped silently. CPython's type_set_bases validates
# the replacement, refuses an inheritance cycle, a layout or deallocator
# change and a duplicate base, then rewrites the MRO of the type and of
# every registered subclass — existing instances follow immediately.
# The super __thisclass__ segment travels with the super-members change
# that owns that descriptor and is not part of this corpus.


class A:
    def who(self): return "A"


class B:
    def who(self): return "B"


class C(A):
    pass


c = C()
print("before", c.who())
C.__bases__ = (B,)
print("after", c.who())
print(C.__bases__)
print([k.__name__ for k in C.__mro__])
print(isinstance(c, A), isinstance(c, B))


class D(C):
    pass


print([k.__name__ for k in D.__mro__])
print(isinstance(D(), B))


class A2:
    def f(self): return "A2.f"


class B2(A2):
    def f(self): return "B2.f+" + super().f()


class Z2:
    def f(self): return "Z2.f"


b2 = B2()
print(b2.f())
B2.__bases__ = (Z2,)
print(b2.f())
print([k.__name__ for k in B2.__mro__])


def try_set(value):
    try:
        C.__bases__ = value
        print("accepted", getattr(value, "__name__", value))
    except TypeError as e:
        print("TypeError:", e)


try_set(5)
try_set(())
try_set((42,))
try_set((C,))
try_set((dict,))
try_set((int,))
try_set((A,))
try_set((A, A))


class X:
    pass


class Y:
    pass


class E0(X, Y):
    pass


class F(C, E0):
    pass


try:
    C.__bases__ = (Y, X)
except TypeError as e:
    print("TypeError:", e)
print([k.__name__ for k in C.__mro__])
print([k.__name__ for k in F.__mro__])

events = []


class WithInitSubA:
    def __init_subclass__(cls, **kw):
        events.append("a")


class WithInitSubB:
    def __init_subclass__(cls, **kw):
        events.append("b")


class WithInitSub(WithInitSubA):
    pass


WithInitSub.__bases__ = (WithInitSubB,)
print(events, WithInitSub.__init_subclass__ is WithInitSubB.__init_subclass__)

try:
    del C.__bases__
except TypeError as e:
    print("TypeError:", e)

try:
    int.__bases__ = (str,)
except TypeError as e:
    print("TypeError:", e)

try:
    (1.5).real = 2
except AttributeError as e:
    print("AttributeError:", e)


try:
    C.__mro__ = ()
except AttributeError as e:
    print("AttributeError:", e)
