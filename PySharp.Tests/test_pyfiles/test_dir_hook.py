"""
dir(instance) honors a custom __dir__ hook resolved through
the type's MRO, sorting the result without dedup and accepting any
iterable. A non-descriptor __dir__ entry in the class dict is called
as-is — non-callables raise the CPython TypeError instead of silently
falling back to the default attribute enumeration.

CPython 3.14 reference (Python/bltinmodule.c builtin_dir, Objects/object.c
PyObject_Dir: _PyObject_LookupSpecial('__dir__'), call no-args, then
PySequence_List + sort).

:kind: test
"""

# the issue repro: hook result is sorted
class C:
    def __dir__(self):
        return ['zeta', 'alpha']
assert dir(C()) == ['alpha', 'zeta']

# tuple results are accepted the same way
class T:
    def __dir__(self):
        return ('zeta', 'alpha')
assert dir(T()) == ['alpha', 'zeta']

# duplicates are preserved in original element order, not deduplicated
class D:
    def __dir__(self):
        return ['a', 'a', 'b']
assert dir(D()) == ['a', 'a', 'b']

# any iterable: a string expands per character, a generator is consumed
class S:
    def __dir__(self):
        return "cba"
assert dir(S()) == ['a', 'b', 'c']

class G:
    def __dir__(self):
        yield "b"
        yield "a"
assert dir(G()) == ['a', 'b']

# inherited hooks resolve through the MRO
class Base:
    def __dir__(self):
        return ['z', 'y']
class Child(Base):
    pass
assert dir(Child()) == ['y', 'z']

# instance-level __dir__ is ignored: the lookup walks the type MRO only
inst = Child()
inst.__dir__ = lambda: ['nope']
assert dir(inst) == ['y', 'z']

# hook errors propagate unmodified
class R:
    def __dir__(self):
        raise ValueError("boom")
try:
    dir(R())
except ValueError as e:
    assert str(e) == "boom"
else:
    assert False, "ValueError expected"

# non-iterable results and mixed-type sorting raise TypeError
class N:
    def __dir__(self):
        return 5
try:
    dir(N())
except TypeError as e:
    assert str(e) == "'int' object is not iterable", str(e)
else:
    assert False, "TypeError expected"

class M:
    def __dir__(self):
        return [1, 'a']
try:
    dir(M())
except TypeError as e:
    assert str(e) == "'<' not supported between instances of 'str' and 'int'", str(e)
else:
    assert False, "TypeError expected"

# a non-descriptor __dir__ in the class dict is called as-is: non-callables
# raise TypeError, non-descriptor callables run without arguments
class NonCallable:
    __dir__ = 42
try:
    dir(NonCallable())
except TypeError as e:
    assert str(e) == "'int' object is not callable", str(e)
else:
    assert False, "TypeError expected"

class Unbound:
    def __call__(self):
        return ['m', 'n']
class Bound:
    __dir__ = Unbound()
assert dir(Bound()) == ['m', 'n']

# dir(C) on the class object does not dispatch the class-body hook; the
# default merge still lists __dir__ itself
class C7:
    def __dir__(self):
        return ['hookonly']
names = dir(C7)
assert 'hookonly' not in names, names
assert '__dir__' in names, names

# staticmethod hooks bind through the descriptor protocol
class C9:
    __dir__ = staticmethod(lambda: ['q', 'p'])
assert dir(C9()) == ['p', 'q']

print("test_dir_hook passed")
