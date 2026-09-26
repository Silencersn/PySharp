"""
container equality and lookup go through the CPython
PyObject_RichCompareBool identity shortcut — an identical object is
always equal (Py_EQ) / never unequal (Py_NE) before any user __eq__
runs. A NaN therefore finds itself in its own containers, and a
raising __eq__ is never consulted for identical operands. Direct
operators (==, !=, operator.eq) keep full-comparison semantics.

CPython 3.14 reference (Objects/object.c PyObject_RichCompareBool;
list/tuple richcompare and searches, dict/set lookups,
_PySequence_IterSearch, slice_richcompare tuple packing).

:kind: test
"""

n = float("nan")

# the issue repro: a NaN must find itself in its own containers
assert n in [n]
assert [n] == [n]
assert (n,) == (n,)
assert n in {n}
assert {n} == {n}
assert {n: 1}[n] == 1
assert (n not in [n]) is False

# controls: direct operators do NOT take the shortcut
assert (n == n) is False
assert (n != n) is True
m = float("nan")
assert ([n] == [m]) is False

# a raising __eq__ is never reached for identical operands
class Raising:
    def __eq__(self, other):
        raise AssertionError("__eq__ must not be consulted")
    def __hash__(self):
        return 7

r = Raising()
assert r in [r]
assert [r] == [r]
assert (r,) == (r,)
assert {r: 1}[r] == 1
assert [r] in [[r]]
assert not ((r,) < (r,))

# __eq__ always returning False still cannot hide an identical element
class AlwaysFalse:
    def __eq__(self, other):
        return False

j = AlwaysFalse()
assert [j].index(j) == 0
assert [j, j].count(j) == 2
lst = [j]
lst.remove(j)
assert lst == []
assert j in (j,)

# dict key lookup: hash bucket hit then identity equality
class Key:
    def __hash__(self):
        return 7
    def __eq__(self, other):
        return False

k = Key()
d = {k: "v"}
assert d[k] == "v"

# dict equality compares values through the same Bool semantics: the same
# NaN object in both dicts is equal (full == would report False)
d1 = {1: n}
d2 = {1: n}
assert d1 == d2

# dict_items membership compares the stored value with the shortcut
class RaisingValue:
    def __eq__(self, other):
        raise AssertionError("__eq__ must not be consulted")

rv = RaisingValue()
items = {1: rv}
assert (1, rv) in items.items()

# slice equality packs the parts into a tuple: member-level identity
# shortcut applies (slice_richcompare via PyObject_RichCompare)
assert slice(n, 1, 2) == slice(n, 1, 2)

# generic iteration search (objects without __contains__) shares the
# RichCompareBool semantics
class Box:
    def __init__(self, items):
        self._items = items
    def __iter__(self):
        return iter(self._items)

assert "two" in Box([1, "two", 3.0])
assert r in Box([r])

# sort only ever consults __lt__, identical elements included
calls = []
class Sortable:
    def __eq__(self, other):
        calls.append("eq")
        return False
    def __lt__(self, other):
        calls.append("lt")
        return False

[Sortable(), Sortable(), Sortable()].sort()
assert "eq" not in calls, calls

# iter(callable, sentinel) stops by identity without consulting __eq__
it = iter(lambda: r, r)
try:
    next(it)
except StopIteration:
    pass
else:
    assert False, "sentinel should stop the iterator"

print("test_container_identity_shortcut passed")
