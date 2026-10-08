"""
Starred bases and **-keyword unpacking in class headers

CPython's class_def grammar takes a full argument list, so *iterables
spread into the base tuple and **mappings merge into the keyword
arguments passed to the metaclass. Mixed explicit bases, starred
iterables and keyword unpacking keep their source order; a starred
generic class takes the same path.

:kind: test
"""

class Base:
    pass


# a lone starred argument is the base tuple itself
class Lone(*[Base]):
    pass

print("lone:", [b.__name__ for b in Lone.__bases__])
print("lone subclass:", issubclass(Lone, Base))


# explicit bases and starred iterables mix, in order
class Mixed(Base, *(object,), *[]):
    pass

print("mixed:", [b.__name__ for b in Mixed.__bases__])


# generators and other iterables unpack too
def make_bases():
    yield Base


class FromGen(*make_bases()):
    pass

print("from gen:", issubclass(FromGen, Base))


# **mappings carry metaclass keywords
kwargs = {"metaclass": type}


class WithMeta(**kwargs):
    pass

print("with meta:", type(WithMeta).__name__)


# starred bases combine with keyword unpacking
class Both(*[Base], **kwargs):
    pass

print("both:", type(Both).__name__, issubclass(Both, Base))


# an empty starred iterable leaves the implicit object base
class Empty(*()):
    pass

print("empty:", [b.__name__ for b in Empty.__bases__])


# generic classes take the same path; the base tuple itself is a
# typing.Generic story, so only the subclass relation is observed here
class Generic[T](*()):
    pass

print("generic:", issubclass(Generic, object))


# a **-merge clash is reported against the build-class machinery, the
# same callable CPython names on this path
try:
    class Dup(x=1, **{"x": 2}):
        pass
except TypeError as e:
    print("dup:", e)
