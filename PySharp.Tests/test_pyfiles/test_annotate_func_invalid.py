"""A malformed __annotate_func__ entry in a class dict degrades to an empty
__annotations__ dict on read, exactly like CPython's non-callable face —
it must never leak a host exception or crash the interpreter.

:kind: test
"""

# malformed payload written inside the class body
class C:
    __annotate_func__ = (1, 2)


print(C.__annotations__)

# non-tuple junk degrades the same way
class C2:
    __annotate_func__ = "not a payload"


print(C2.__annotations__)

# a body with real annotations whose payload is then overwritten
class C3:
    x: int
    __annotate_func__ = (1, 2, 3, 4)


print(C3.__annotations__)

# assignment after class creation
class D:
    x: int


D.__annotate_func__ = (1, 2, 3, 4)
print(D.__annotations__)

# runtime type() namespace
E = type("E", (), {"__annotate_func__": (1, 2, 3)})
print(E.__annotations__)

# a subclass body entry shadows the base's valid payload
class B:
    x: int


class F(B):
    __annotate_func__ = (1, 2)


print(F.__annotations__)

# the base is unaffected, and a real payload still evaluates
print(B.__annotations__["x"] is int)


class G:
    y: str


print(G.__annotations__["y"] is str)
