"""
the sequence protocol family (sq_concat/sq_repeat and their
in-place variants) lives on its own PyTypeSlots.Sequence group, with the
CPython slotdefs "one name, many slots" semantics:

- a dunder name fills the number-side slot and NULLs the sequence-side
  slot for heap types (typeobject.c:11131 — sq_concat/sq_repeat carry
  function=NULL, the abstract layer falls back nb -> sq);
- the abstract-layer fallback directions: PyNumber_Add consults only the
  LEFT operand's sq_concat; PyNumber_Multiply tries the left sq_repeat,
  else the right one (Objects/abstract.c:1128/1165);
- deleting an overriding __add__ restores the inherited native wrapper
  through the specific path (wrapper delegate identity against the
  Sequence slot — the d_base->wrapper analog);
- native sequence types expose no __radd__ (sq_concat has no reflected
  variant) but keep __rmul__ with the non-swapping wrap_indexargfunc
  order, provided as hand-written RMul overrides over Repeat.

:kind: test
"""


# --- native dispatch directions ---

assert "a" + "b" == "ab"
assert "ab" * 2 == "abab"
assert 3 * "ab" == "ababab"          # right-side sq_repeat fallback
assert [1] + [2] == [1, 2]
assert [1] * 2 == [1, 1]
assert 2 * [1] == [1, 1]             # right-side sq_repeat fallback
assert (1,) + (2,) == (1, 2)
assert b"a" + b"b" == b"ab"
assert bytearray(b"a") + bytearray(b"b") == bytearray(b"ab")

l = [1]
l += [2]
assert l == [1, 2]
l *= 2
assert l == [1, 2, 1, 2]
s = "a"
s += "b"
assert s == "ab"
s *= 3
assert s == "ababab"
ba = bytearray(b"a")
ba += b"b"
assert ba == bytearray(b"ab")
ba *= 2
assert ba == bytearray(b"abab")

# reflected user __radd__ still runs when the left operand is a native
# sequence (str has no nb_add; the right operand's nb_add carries __radd__)
class RAdder:
    def __radd__(self, other):
        return "R:" + other


assert "a" + RAdder() == "R:a"

# --- attribute surface of the native sequence types ---

assert not hasattr(str, "__radd__")      # sq_concat has no reflected variant
assert not hasattr(list, "__radd__")
assert hasattr(str, "__rmul__")          # sq_repeat's non-swapping __rmul__
assert hasattr(bytes, "__rmul__")
assert hasattr(bytearray, "__rmul__")
assert hasattr(tuple, "__rmul__")
# set has neither nb_add nor sq_concat: + must fail
try:
    set() + set()
    assert False, "set + must fail"
except TypeError:
    pass

# --- heap NULL rule (typeobject.c:11131) ---


class OverridingStr(str):
    __add__ = lambda self, other: NotImplemented


try:
    result = OverridingStr("a") + OverridingStr("b")
    assert False, f"expected TypeError, got {result!r}"
except TypeError:
    # the sequence-side slot is nulled by the user definition, so the
    # NotImplemented decline is terminal — no inherited-concat fallback
    pass


class OverridingList(list):
    __add__ = lambda self, other: NotImplemented


try:
    result = OverridingList([1]) + OverridingList([2])
    assert False, f"expected TypeError, got {result!r}"
except TypeError:
    pass


# a user class with __mul__ must not reach any inherited sq slot either
class MulDecliner:
    def __mul__(self, other):
        return NotImplemented


try:
    result = MulDecliner() * 2
    assert False, f"expected TypeError, got {result!r}"
except TypeError:
    pass

# --- specific-path restoration after del ---


class DeletedStr(str):
    __add__ = lambda self, other: NotImplemented


del DeletedStr.__add__
assert DeletedStr("a") + DeletedStr("b") == "ab"

# mutation propagation keeps the same semantics across an existing subtree


class MidStr(str):
    pass


class LeafStr(MidStr):
    pass


MidStr.__add__ = lambda self, other: NotImplemented
try:
    MidStr("a") + MidStr("b")
    assert False, "propagated override must be terminal"
except TypeError:
    pass
try:
    LeafStr("a") + LeafStr("b")
    assert False, "propagated override must cover the subtree"
except TypeError:
    pass

del MidStr.__add__
assert MidStr("a") + MidStr("b") == "ab"
assert LeafStr("a") + LeafStr("b") == "ab"

# --- runtime subclasses keep native concat by reference ---


class PlainStr(str):
    pass


class PlainList(list):
    pass


assert PlainStr("a") + PlainStr("b") == "ab"
assert PlainList([1]) + PlainList([2]) == [1, 2]
assert PlainStr("a") * 2 == "aa"
assert 2 * PlainStr("a") == "aa"

print("ok")
