"""
attribute writes and deletes on dict-less (frozen) instances
raise catchable Python AttributeErrors instead of leaking a .NET
NotSupportedException from the frozen attributes stub.

DefaultSetAttr/DefaultDelAttr reject the write on IsImmutable instances
with the two CPython message shapes: a name found in the MRO without a
setter is "read-only", anything else is the shared "no attribute and no
__dict__" error (assignment and deletion alike). Mutable instances
(subclasses with a dict) and data-descriptor dispatch are unaffected.

CPython 3.14 reference (Objects/object.c _PyObject_GenericSetAttrWithDict).

:kind: test
"""

NO_DICT = " and no __dict__ for setting new attributes"

# the issue repro: frozen built-in precise instances, set with no MRO entry
try:
    "x".foo = 1
except AttributeError as e:
    assert str(e) == "'str' object has no attribute 'foo'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    (1).foo = 2
except AttributeError as e:
    assert str(e) == "'int' object has no attribute 'foo'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    b"".x = 1
except AttributeError as e:
    assert str(e) == "'bytes' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    ().x = 1
except AttributeError as e:
    assert str(e) == "'tuple' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    (1.5).x = 1
except AttributeError as e:
    assert str(e) == "'float' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    True.x = 1
except AttributeError as e:
    assert str(e) == "'bool' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    (1j).x = 1
except AttributeError as e:
    assert str(e) == "'complex' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    frozenset().x = 1
except AttributeError as e:
    assert str(e) == "'frozenset' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

# a name found in the MRO without a setter is reported read-only
try:
    "x".upper = 1
except AttributeError as e:
    assert str(e) == "'str' object attribute 'upper' is read-only", str(e)
else:
    assert False, "AttributeError expected"

assert "x".upper() == "X"

# __dict__ has no descriptor on the MRO for frozen instances, so it takes
# the shared no-dict shape
try:
    "x".__dict__ = {}
except AttributeError as e:
    assert str(e) == "'str' object has no attribute '__dict__'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

# deletion shares the same two shapes in CPython
try:
    del "x".foo
except AttributeError as e:
    assert str(e) == "'str' object has no attribute 'foo'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    del "x".upper
except AttributeError as e:
    assert str(e) == "'str' object attribute 'upper' is read-only", str(e)
else:
    assert False, "AttributeError expected"

# object() instances have no dict either
try:
    object().x = 1
except AttributeError as e:
    assert str(e) == "'object' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

try:
    del object().x
except AttributeError as e:
    assert str(e) == "'object' object has no attribute 'x'" + NO_DICT, str(e)
else:
    assert False, "AttributeError expected"

# __class__ assignment is rejected catchably; CPython raises TypeError from
# the __class__ setter itself — full alignment is a documented follow-up
try:
    "x".__class__ = str
except Exception:
    pass
else:
    assert False, "catchable error expected"

# data-descriptor dispatch still precedes the frozen gate: float.real has a
# setter stub raising its own error (CPython says "attribute 'real' of
# 'float' objects is not writable" — documented divergence, not pinned here)
try:
    (1.5).real = 1
except Exception:
    pass
else:
    assert False, "catchable error expected"
assert (1.5).real == 1.5

# mutable instances keep working: subclasses carry a real instance dict
class S(str):
    pass

s = S("x")
s.foo = 1
assert s.foo == 1
del s.foo
assert not hasattr(s, "foo")

class L(list):
    pass

l = L([1, 2])
l.attr = "ok"
assert l.attr == "ok"

class C:
    pass

c = C()
c.v = 42
assert c.v == 42
del c.v
assert not hasattr(c, "v")

# python-level property without setter/deleter reports through the property
# descriptor itself, unchanged by the frozen gate
class P:
    p = property(lambda self: 1)

try:
    P().p = 2
except AttributeError as e:
    assert str(e) == "property 'p' of 'P' object has no setter", str(e)
else:
    assert False, "AttributeError expected"

try:
    del P().p
except AttributeError as e:
    assert str(e) == "property 'p' of 'P' object has no deleter", str(e)
else:
    assert False, "AttributeError expected"

print("test_frozen_instance_setattr: all assertions passed")
