"""
class __dict__ is a read-only mappingproxy: reads see the live namespace,
while every mutation is rejected and must go through setattr/delattr.

CPython 3.14 reference:
    type(C.__dict__).__name__  -> 'mappingproxy'
    C.__dict__ is C.__dict__   -> False (a fresh proxy per read)
    C.__dict__['y'] = 2        -> TypeError: 'mappingproxy' object does not
                                  support item assignment
    del C.__dict__['x']        -> TypeError: 'mappingproxy' object does not
                                  support item deletion
    m.update / m.pop / ...     -> AttributeError
    int.__dict__               -> a mappingproxy as well
    module and instance __dict__ -> still a plain mutable dict

:kind: test
:background: Item assignment on class __dict__ used to succeed and rewrite
    the class structure silently; CPython rejects it through the
    mappingproxy face.
"""

class C:
    x = 1

m = C.__dict__

# type name and per-read identity
assert type(m).__name__ == 'mappingproxy'
assert not isinstance(m, dict)
assert C.__dict__ is not m
assert C.__dict__ == m
assert vars(C) == m
assert vars(C) is not m

# reads see the live namespace
assert m['x'] == 1
assert m.get('x') == 1
assert m.get('nope', 5) == 5
assert 'x' in m
assert 'nope' not in m
assert len(m) >= 1
assert 'x' in list(m.keys())
assert 1 in list(m.values())
assert ('x', 1) in list(m.items())
assert bool(m)

# repr carries the wrapper, str prints like the wrapped dict
assert repr(m).startswith('mappingproxy({')
assert str(m).startswith('{')

# copies are plain dicts and independent
cp = m.copy()
assert type(cp).__name__ == 'dict'
assert cp == m
cp['x'] = 9
assert C.x == 1
assert m['x'] == 1

# dict() over the proxy works through the mapping protocol
assert dict(m)['x'] == 1

# item assignment is rejected and leaves the class untouched
try:
    m['y'] = 2
    raise AssertionError('class dict item assignment accepted')
except TypeError as e:
    assert str(e) == "'mappingproxy' object does not support item assignment", str(e)
assert not hasattr(C, 'y')

# item deletion is rejected
try:
    del m['x']
    raise AssertionError('class dict item deletion accepted')
except TypeError as e:
    assert str(e) == "'mappingproxy' object does not support item deletion", str(e)
assert C.x == 1

# the mutation methods of dict are simply absent
for name in ('update', 'pop', 'clear', 'setdefault', 'popitem'):
    try:
        getattr(m, name)
        raise AssertionError('mappingproxy exposes ' + name)
    except AttributeError:
        pass

# |= is rejected with the use-| hint
try:
    m |= {'z': 3}
    raise AssertionError('mappingproxy |= accepted')
except TypeError as e:
    assert str(e) == "'|=' is not supported by mappingproxy; use '|' instead", str(e)

# hashing delegates to the wrapped dict, which is unhashable
try:
    hash(m)
    raise AssertionError('mappingproxy hashed')
except TypeError as e:
    assert str(e) == "unhashable type: 'dict'", str(e)

# comparisons run against the wrapped mapping
assert m == m.copy()
d = dict(m)
assert m == d
assert d == m
assert m != {'nope': 0}

# missing keys raise KeyError
try:
    m['nope']
    raise AssertionError('missing key read accepted')
except KeyError:
    pass

# match treats the proxy as a mapping
def match_mapping(obj):
    match obj:
        case {'x': v}:
            return v
    return None

assert match_mapping(C.__dict__) == 1

# writes through setattr/delattr stay visible through the proxy
C.w = 3
assert m['w'] == 3
assert 'w' in list(m.keys())
del C.w
assert 'w' not in m
assert not hasattr(C, 'w')

# built-in types use the same proxy face
assert type(int.__dict__).__name__ == 'mappingproxy'

# subclass namespaces hold only their own class body
class D(C):
    y = 2

assert 'y' in D.__dict__
assert 'x' not in D.__dict__
assert 'y' not in C.__dict__
assert C.__dict__['x'] == 1

# construction accepts mappings, rejects other types, nests proxies
mp = type(m)
p2 = mp({'q': 7})
assert type(p2).__name__ == 'mappingproxy'
assert p2['q'] == 7
assert mp(p2)['q'] == 7
try:
    mp([1])
    raise AssertionError('mappingproxy accepted a list')
except TypeError as e:
    assert str(e) == 'mappingproxy() argument must be a mapping, not list', str(e)
try:
    mp(42)
    raise AssertionError('mappingproxy accepted an int')
except TypeError as e:
    assert str(e) == 'mappingproxy() argument must be a mapping, not int', str(e)

# the type itself is not exposed as a builtin name
import builtins
assert not hasattr(builtins, 'mappingproxy')

# module and instance __dict__ keep their plain mutable dicts
import math
assert type(math.__dict__).__name__ == 'dict'

class Obj:
    pass

o = Obj()
o.a = 1
assert type(o.__dict__).__name__ == 'dict'
o.__dict__['b'] = 2
assert o.b == 2

print("test_class_dict_mappingproxy passed")
