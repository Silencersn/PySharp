"""BaseException.args is a writable member: assigned iterables drain into a fresh tuple (assigned tuples keep identity), non-iterables raise the plain iteration error, deletion is rejected, and str()/repr() reflect rewrites immediately.

:kind: test
"""
# Regression: BaseException.args is a writable member.
#
# CPython BaseException_set_args (Objects/exceptions.c) routes any value
# through PySequence_Tuple: a tuple is kept by identity, any other
# iterable is drained into a fresh tuple, and a non-iterable fails with
# the plain iteration error. Deletion is rejected with
# "args may not be deleted". str()/repr() read args live, so rewriting
# args after a catch is visible immediately.
e = ValueError("v")
e.args = (1, 2)
assert repr(e) == "ValueError(1, 2)", repr(e)
assert str(e) == "(1, 2)", str(e)

e.args = ()
assert e.args == () and str(e) == "", repr(e.args)

e.args = [1, 2]
assert e.args == (1, 2) and type(e.args) is tuple, repr(e.args)

e.args = "abc"
assert e.args == ("a", "b", "c"), repr(e.args)

e.args = range(3)
assert e.args == (0, 1, 2), repr(e.args)

e.args = (x for x in (1, 2))
assert e.args == (1, 2), repr(e.args)

t = (7, 8)
e.args = t
assert e.args is t, "an assigned tuple must come back by identity"

try:
    e.args = 5
    assert False, "int is not iterable"
except TypeError as ex:
    assert str(ex) == "'int' object is not iterable", str(ex)

try:
    del e.args
    assert False, "args may not be deleted"
except TypeError as ex:
    assert str(ex) == "args may not be deleted", str(ex)

# The KeyError single-argument repr form follows the rewritten args
k = KeyError("k")
k.args = ("other",)
assert str(k) == "'other'", str(k)
assert repr(k) == "KeyError('other')", repr(k)

# The classic rewrite-and-reraise pattern from the issue report
try:
    raise ValueError("first", 42)
except ValueError as ex:
    ex.args = ("second",) + ex.args[1:]
    assert ex.args == ("second", 42), repr(ex.args)

# Subclass instances inherit the setter
class MyError(ValueError):
    pass

m = MyError("a")
m.args = ("b",)
assert str(m) == "b" and repr(m) == "MyError('b')", repr(m)

# Multi-argument tuples keep tuple str semantics
m.args = (1, 2, 3)
assert str(m) == "(1, 2, 3)", str(m)

print("ok")
