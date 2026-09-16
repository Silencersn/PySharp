"""
Regression: container subclass construction dispatch.

CPython splits construction into tp_new (allocates an empty object and,
for immutable types, consumes the arguments) and tp_init (consumes the
arguments for mutable dict/list/set). A Python subclass __init__ replaces
the built-in init slot, so dict/list/set subclasses receive the constructor
arguments instead of the builtins iterating the first one as key-value
pairs. tuple/frozenset keep consuming the iterable inside __new__ and then
still call the subclass __init__. __new__/__init__ are resolved by MRO
lookup, not by class-creation order, so a sibling base defining __init__
wins over a base created earlier whose slot was already filled with the
built-in. Direct __init__ calls re-initialize: list/set clear, dict merges.

CPython 3.14 reference (Objects/dictobject.c dict_new/dict_init,
Objects/listobject.c list.__init__/PyType_GenericNew,
Objects/setobject.c set_new/set_init, Objects/tupleobject.c
tuple_subtype_new, typeobject.c type_call/slot_tp_init).
"""

# --- dict subclass with a custom __init__ (the reported case) ---
class DD(dict):
    def __init__(self, factory):
        self.factory = factory

d = DD(list)
assert d.factory is list, "custom __init__ must receive the argument"
assert dict(d) == {}, "built-in iteration must not consume the argument"
assert dict(DD([('a', 1)])) == {}, "pairs must go to __init__, not the dict"

class D2(dict):
    def __init__(self, factory):
        self.factory = factory
        dict.__init__(self, [('k', 1)])

d2 = D2(list)
assert d2.factory is list and dict(d2) == {'k': 1}

class DD2(dict):
    def __init__(self):
        self.mark = 1

assert DD2().mark == 1
try:
    DD2([('a', 1)])
    assert False, "extra argument must reach the Python __init__"
except TypeError:
    pass

class DD3(dict):
    def __init__(self):
        return 5

try:
    DD3()
    assert False, "__init__ must return None"
except TypeError:
    pass

# --- subclasses without __init__ keep the built-in update semantics ---
class DN(dict):
    pass

assert dict(DN(a=1)) == {'a': 1}
assert dict(DN([('a', 1)], b=2)) == {'a': 1, 'b': 2}

try:
    DN(1, 2)
    assert False
except TypeError as e:
    assert str(e) == "dict expected at most 1 argument, got 2", str(e)
try:
    DN(1)
    assert False
except TypeError as e:
    assert str(e) == "'int' object is not iterable", str(e)

# custom __new__ without __init__: the inherited built-in init still runs
class DN2(dict):
    def __new__(cls, x):
        return dict.__new__(cls)

assert dict(DN2([('a', 1)])) == {'a': 1}

# direct dict.__init__ merges and keyword updates override
d0 = {'z': 9}
dict.__init__(d0, [('y', 1)], n=3)
assert d0 == {'z': 9, 'y': 1, 'n': 3}
d1 = {'a': 1}
dict.__init__(d1, [('a', 5)], a=7)
assert d1 == {'a': 7}

# --- list subclass ---
class LL(list):
    def __init__(self, factory):
        self.factory = factory

l = LL(dict)
assert l.factory is dict and list(l) == []

class LN(list):
    pass

assert list(LN([1, 2])) == [1, 2]
try:
    list(1, 2)
    assert False
except TypeError as e:
    assert str(e) == "list expected at most 1 argument, got 2", str(e)
try:
    list(a=1)
    assert False
except TypeError as e:
    assert str(e) == "list() takes no keyword arguments", str(e)

l0 = [1, 2]
list.__init__(l0, [5])
assert l0 == [5], "re-initialization clears previous contents"
list.__init__(l0)
assert l0 == []

# --- set subclass ---
class SS(set):
    def __init__(self, factory):
        self.factory = factory

s = SS(dict)
assert s.factory is dict and set(s) == set()

class SN(set):
    pass

assert SN([1, 2]) == {1, 2}
try:
    set(1)
    assert False
except TypeError as e:
    assert str(e) == "'int' object is not iterable", str(e)
try:
    set([1], [2])
    assert False
except TypeError as e:
    assert str(e) == "set expected at most 1 argument, got 2", str(e)
try:
    set(a=1)
    assert False
except TypeError as e:
    assert str(e) == "set() takes no keyword arguments", str(e)

s0 = {1}
set.__init__(s0, [7])
assert s0 == {7}

# --- tuple subclass: __new__ consumes the iterable, __init__ still runs ---
class TT(tuple):
    def __init__(self, factory):
        self.factory = factory

t = TT('ab')
assert tuple(t) == ('a', 'b') and t.factory == 'ab'
try:
    TT(dict)
    assert False
except TypeError as e:
    assert str(e) == "'type' object is not iterable", str(e)

# --- frozenset subclass: same, and kwargs are forwarded to __init__ ---
class FF(frozenset):
    def __init__(self, factory):
        self.factory = factory

f = FF('ab')
assert set(f) == {'a', 'b'} and f.factory == 'ab'

class FN(frozenset):
    pass

assert FN('ab') == frozenset('ab')
try:
    frozenset(a=1)
    assert False
except TypeError as e:
    assert str(e) == "frozenset() takes no keyword arguments", str(e)

# --- __init__ resolved by MRO, not class-creation order ---
class M1(dict):
    def __init__(self, f):
        self.f = 'm1'

class M2(dict):
    pass

class M3(M2, M1):
    pass

assert M3('x').f == 'm1' and dict(M3('x')) == {}

# inherited through a plain intermediate subclass
class Base(dict):
    def __init__(self, f):
        self.f = f

class Sub(Base):
    pass

assert Sub('x').f == 'x' and dict(Sub('x')) == {}

# __init__ assigned after class creation still wins the dispatch
class DE(dict):
    pass

DE.__init__ = lambda self, f: setattr(self, 'f', f)
assert DE(5).f == 5 and dict(DE(5)) == {}

# cooperative init chains keep working
class OD(dict):
    def __init__(self, seq=()):
        dict.__init__(self, seq)
        self.mark = 'od'

assert dict(OD([('a', 1)])) == {'a': 1} and OD([('b', 2)]).mark == 'od'

# defaultdict simulation: the motivating use case
class DefDict(dict):
    def __init__(self, default_factory):
        self.default_factory = default_factory

    def __missing__(self, key):
        v = self.default_factory()
        self[key] = v
        return v

dd = DefDict(list)
dd['x'].append(1)
assert dd == {'x': [1]}

# exact-type constructors unchanged
assert dict([('a', 1)], b=2) == {'a': 1, 'b': 2}
assert list('ab') == ['a', 'b']
assert set('ab') == {'a', 'b'}
assert dict({'a': 1}, b=2) == {'a': 1, 'b': 2}
assert frozenset({1, 2}) == frozenset([1, 2])

print("container subclass init regression passed")
