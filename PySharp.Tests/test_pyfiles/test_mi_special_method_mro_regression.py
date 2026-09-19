# Regression: multiple inheritance with a non-first parent defining a
# special method that object also gives a default for (__ne__, __gt__,
# __ge__, __hash__, ...). Inherited copies of the object defaults baked
# into earlier bases' slots masked the later base's real method, so
# `!=` fell back to __eq__ inversion / identity, `>` raised TypeError,
# `>=` dispatched the wrong parent's __le__, and __hash__ fell back to
# the id hash. CPython fixup_slot_dispatchers resolves each special
# method through the first MRO dict hit; the slot wiring must do the same.

class EqPart:
    def __eq__(self, o):
        return "from-eq"

class NePart:
    def __ne__(self, o):
        return "from-ne"

class Both(EqPart, NePart):
    pass

b1, b2 = Both(), Both()
assert (b1 == b2) == "from-eq"
assert (b1 != b2) == "from-ne"

# single inheritance stays correct (guard)
class OnlyNe:
    def __ne__(self, o):
        return "from-ne"

class Sub(OnlyNe):
    pass
assert (Sub() != Sub()) == "from-ne"

# reversed base order: the first MRO dict hit still wins
class Both2(NePart, EqPart):
    pass
assert (Both2() != Both2()) == "from-ne"

# an unrelated base between the two providers
class Plain:
    pass

class Both3(EqPart, Plain, NePart):
    pass
assert (Both3() != Both3()) == "from-ne"

# the defining dict sits two MRO levels below the direct bases
class SubNe(OnlyNe):
    pass

class Mid(SubNe):
    pass

class Deep(EqPart, Mid):
    pass
assert (Deep() != Deep()) == "from-ne"

# __gt__ from a non-first parent
class EqFirst:
    def __eq__(self, o):
        return True

class GtSecond:
    def __gt__(self, o):
        return "gt-from-second"

class GtBoth(EqFirst, GtSecond):
    pass
assert (GtBoth() > GtBoth()) == "gt-from-second"

# __ge__ and __le__ dispatch to their own parents, not each other
class LeHolder:
    def __le__(self, o):
        return "le-second"

class GeHolder:
    def __ge__(self, o):
        return "ge-second"

class MI(LeHolder, GeHolder):
    pass
assert (MI() >= MI()) == "ge-second"
assert (MI() <= MI()) == "le-second"
try:
    MI() < MI()
    assert False, "expected TypeError"
except TypeError:
    pass

# __hash__ from a non-first parent
class HashMixin:
    def __hash__(self):
        return 500

class Empty:
    pass

class Child(Empty, HashMixin):
    pass
assert hash(Child()) == 500

# a class defining __eq__ stays unhashable even with a hash mixin behind it
class DefinesEq:
    def __eq__(self, o):
        return True

class Unhashable(DefinesEq, HashMixin):
    pass
try:
    hash(Unhashable())
    assert False, "expected TypeError"
except TypeError:
    pass

# the inherited __hash__ = None marker propagates through the MRO
try:
    hash(Both())
    assert False, "expected TypeError"
except TypeError:
    pass

# a __ne__ returning NotImplemented on both sides falls back to the
# identity check — do_richcompare's Py_NE default never consults __eq__
# (the __eq__ inversion belongs to object.__ne__, reached by slot
# dispatch only when no __ne__ is defined anywhere)
class NeNotImpl:
    def __ne__(self, o):
        return NotImplemented

class EqTrue:
    def __eq__(self, o):
        return True

class FallNe(NeNotImpl, EqTrue):
    pass
assert (FallNe() != FallNe()) is True

class FallNe2(EqTrue, NeNotImpl):
    pass
assert (FallNe2() != FallNe2()) is True

# without any user __ne__, object.__ne__ still inverts the __eq__ result
class OnlyEq:
    def __eq__(self, o):
        return True
assert (OnlyEq() != OnlyEq()) is False

# the class's own dict still beats inherited providers
class Overriding(NePart):
    def __ne__(self, o):
        return "own"
assert (Overriding() != Overriding()) == "own"

# post-creation assignment on the class re-wires the dispatch
class Assigned(EqPart, NePart):
    pass

Assigned.__ne__ = lambda self, o: "assigned"
assert (Assigned() != Assigned()) == "assigned"

# __repr__ resolution follows the MRO dict order
class ReprA:
    def __repr__(self):
        return "ra"

class ReprB:
    def __repr__(self):
        return "rb"

class ReprBoth(ReprA, ReprB):
    pass
assert repr(ReprBoth()) == "ra"

class NoRepr:
    pass

class ReprLater(NoRepr, ReprB):
    pass
assert repr(ReprLater()) == "rb"

# __lt__ from a non-first parent participates in sorting
class NoLt:
    pass

class HasLt:
    def __init__(self, v):
        self.v = v

    def __lt__(self, o):
        return self.v < o.v

class Sortable(NoLt, HasLt):
    pass
assert [s.v for s in sorted([Sortable(3), Sortable(1), Sortable(2)])] == [1, 2, 3]

# instance-level definitions do not participate in dunder dispatch
inst = Both()
inst.__ne__ = lambda o: "instance"
assert (inst != Both()) == "from-ne"

print("ok")
