# Regression: builtin types synthesize reflected operator wrappers from
# their non-null as_number slots (CPython add_operators). int exposed no
# __radd__..__ror__ at all, so hasattr probes failed and direct calls
# raised AttributeError; the reflection dispatch itself (rightType slot)
# was always correct — only the type-level visibility was missing.

# int: the full reflected family is visible and operand-swapping
assert hasattr(int, "__radd__")
assert (2).__radd__(3) == 5
assert (2).__rsub__(3) == 1
assert (2).__rmul__(3) == 6
assert (2).__rtruediv__(8) == 4.0
assert (2).__rfloordiv__(5) == 2
assert (2).__rmod__(5) == 1
assert (2).__rdivmod__(5) == (2, 1)
assert (2).__rlshift__(3) == 12
assert (2).__rrshift__(13) == 3
assert (2).__rand__(3) == 2
assert (2).__rxor__(3) == 1
assert (2).__ror__(3) == 3
assert (2).__rpow__(3) == 9
assert (2).__rpow__(3, 5) == 4
assert not hasattr(int, "__rmatmul__")

# the ternary wrapper keeps the modulus optional like wrap_ternaryfunc_r
assert (2).__pow__(3) == 8
assert int.__pow__(2, 3) == 8
assert (3.0).__rpow__(2) == 8.0

# in-place slots stay absent for immutable builtins
assert not hasattr(int, "__iadd__")
assert not hasattr(int, "__ipow__")

# bool inherits the wrappers through the MRO instead of synthesizing its
# own (CPython slot_inherited skip: bool.__dict__ has no __radd__)
assert hasattr(bool, "__radd__")
assert "__radd__" not in bool.__dict__
assert bool.__radd__ is int.__radd__
assert True.__radd__(True) == 2

# float/complex keep their hand-written reflected family
assert hasattr(float, "__radd__")
assert hasattr(float, "__rpow__")
assert hasattr(complex, "__radd__")

# sequence concat rides sq_concat in CPython, which has no reflected
# variant — no reflected wrappers for __radd__; sq_repeat keeps its
# __rmul__ entry (wrap_indexargfunc, non-swapping self*value order)
for t in (str, bytes, bytearray, list, tuple):
    assert not hasattr(t, "__radd__"), t
    assert hasattr(t, "__rmul__"), t
assert "ab".__rmul__(3) == "ababab"
assert [1].__rmul__(2) == [1, 1]
assert (1,).__rmul__(2) == (1, 2 - 1,)
assert b"a".__rmul__(2) == b"aa"
assert bytearray(b"a").__rmul__(2) == bytearray(b"aa")
try:
    [1].__rmul__("x")
    assert False, "expected TypeError"
except TypeError:
    pass

# str's __mod__ is an as_number slot, so __rmod__ exists
assert hasattr(str, "__rmod__")

# set has no reflected add: the union operator is __or__, and the
# placeholder Add slot must not grow a __radd__ (set + set is a
# TypeError on both engines)
assert not hasattr(set, "__radd__")
try:
    set() + set()
    assert False, "expected TypeError"
except TypeError:
    pass
assert hasattr(set, "__rsub__")
assert hasattr(set, "__rand__")
assert hasattr(set, "__rxor__")
assert hasattr(set, "__ror__")
assert {1, 2}.__rsub__({1}) == set()
assert hasattr(frozenset, "__rsub__")

# a user class with __mod__ meets str.__rmod__ through the descriptor
# protocol: the wrapper is a bound view of the swapped forward slot
class Modder:
    def __mod__(self, other):
        return "modded"

assert "abc".__rmod__ is not None

# the wrappers are real descriptors binding to instances
bound = (2).__radd__
assert bound(3) == 5

print("ok")
