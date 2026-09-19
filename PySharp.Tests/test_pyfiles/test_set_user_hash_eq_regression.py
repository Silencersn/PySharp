# Set/frozenset storage regression: elements live in PySharp's own
# setentry-style table (stored full 64-bit hash), so user __hash__/__eq__
# run under the live context and failures surface as catchable Python
# exceptions — never .NET crashes. Merge copies (set(s)/copy()/|/union/
# update-from-set/{*s}/frozenset(s)) reuse stored hashes and never re-call
# __hash__; probe comparisons use the stored element as the __eq__
# receiver with an identity shortcut; only direct element faces re-raise
# hash TypeErrors with the set wording while iterable conversions
# propagate raw; set iterators raise "Set changed size during iteration"
# on size changes (same-size mutation allowed, sticky, exhausted
# iterators stay silent), mirroring CPython's si_used check.

def expect(label, fn, exc, msg):
    try:
        fn()
    except exc as e:
        assert str(e) == msg, f"{label}: {str(e)!r} != {msg!r}"
    else:
        assert False, f"{label}: no {exc.__name__}"


class H:
    def __init__(self, v):
        self.v = v

    def __hash__(self):
        return self.v

    def __eq__(self, o):
        return isinstance(o, H) and o.v == self.v


# ---- basic membership, construction, bulk results ----
s = {H(1)}
t = set([H(1), H(2)])
assert len(s) == 1 and len(t) == 2
assert H(1) in s and H(3) not in s

s.add(H(1))
assert len(s) == 1
s.discard(H(9))
assert len(s) == 1


class Plain:
    def __repr__(self):
        return "Plain()"


expect("remove-missing", lambda: {1}.remove(Plain()), KeyError, "Plain()")
s.remove(H(1))
assert len(s) == 0

u = {H(1)}.union({H(2)}, [H(3)])
assert sorted(x.v for x in u) == [1, 2, 3]
i = u.intersection({H(1), H(9)}, {H(1), H(11)})
assert sorted(x.v for x in i) == [1]
d = u.difference({H(1)})
assert sorted(x.v for x in d) == [2, 3]
c = u.copy()
c.add(H(99))
assert H(99) in c and H(99) not in u
assert type(c).__name__ == "set"

x = {H(1)}
x.update({H(2)})
x.symmetric_difference_update({H(2), H(5)})
assert sorted(q.v for q in x) == [1, 5]
y = x.symmetric_difference({H(1)})
assert sorted(q.v for q in y) == [5]
x.intersection_update({H(1), H(5)})
assert sorted(q.v for q in x) == [1, 5]
x.difference_update({H(5)})
assert sorted(q.v for q in x) == [1]

# ---- merge copies never re-call __hash__ (CPython set_merge) ----
class Counting:
    def __init__(self):
        self.n = 0

    def __eq__(self, o):
        return False

    def __hash__(self):
        self.n += 1
        if self.n > 1:
            raise ValueError("boom")
        return 42


k = Counting()
base = {k}
merged = [set(base), base.copy(), base | base, base.union(base), frozenset(base), {*base}]
base.update(base)
assert all(len(m) == 1 for m in merged) and len(base) == 1
assert k.n == 1, f"merge paths re-hashed: {k.n}"
expect("contains-rehash", lambda: k in base, ValueError, "boom")

# ---- hash failures: direct element faces carry the set wording ----
class RH:
    def __eq__(self, o):
        return False

    def __hash__(self):
        raise ValueError("h-boom")


expect("literal", lambda: {RH()}, ValueError, "h-boom")
expect("in-empty", lambda: RH() in set(), ValueError, "h-boom")
expect("in-nonempty", lambda: RH() in {1}, ValueError, "h-boom")
expect("ctor", lambda: set([RH()]), ValueError, "h-boom")
expect("add", lambda: set().add(RH()), ValueError, "h-boom")
expect("remove", lambda: {1}.remove(RH()), ValueError, "h-boom")
expect("discard", lambda: {1}.discard(RH()), ValueError, "h-boom")
expect("frozenset-in", lambda: RH() in frozenset([1]), ValueError, "h-boom")

# only TypeErrors from the hash get the container wording
class TH:
    def __eq__(self, o):
        return False

    def __hash__(self):
        raise TypeError("boom")


expect("wrap-add", lambda: {TH()}, TypeError,
       "cannot use 'TH' as a set element (boom)")
expect("wrap-in", lambda: TH() in {1}, TypeError,
       "cannot use 'TH' as a set element (boom)")

# the wrap embeds str(exc): multi-arg renders as a tuple, non-str via str()
class TM:
    def __eq__(self, o):
        return False

    def __hash__(self):
        raise TypeError("a", "b")


expect("wrap-multi", lambda: TM() in {1}, TypeError,
       "cannot use 'TM' as a set element (('a', 'b'))")


class TN:
    def __eq__(self, o):
        return False

    def __hash__(self):
        raise TypeError(42)


expect("wrap-nonstr", lambda: TN() in {1}, TypeError,
       "cannot use 'TN' as a set element (42)")

# unhashable builtins keep the standard wording on direct faces
expect("in-list", lambda: [] in {1}, TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")

# iterable conversions split like CPython: the set_intersection path
# (issubset / intersection with a non-set iterable) hashes inline and
# propagates raw; everything else hashes at the key level with the wrap
expect("issubset-raw", lambda: {1}.issubset([[]]), TypeError,
       "unhashable type: 'list'")
expect("intersection-raw", lambda: {1}.intersection([[]]), TypeError,
       "unhashable type: 'list'")
expect("intersection-update-raw", lambda: {1}.intersection_update([[]]), TypeError,
       "unhashable type: 'list'")
expect("update-wrap", lambda: {1}.update([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("fs-ctor-wrap", lambda: frozenset([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("union-wrap", lambda: {1}.union([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("issuperset-wrap", lambda: {1}.issuperset([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("isdisjoint-wrap", lambda: {1}.isdisjoint([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("diff-update-wrap", lambda: {1}.difference_update([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("symdiff-wrap", lambda: {1}.symmetric_difference([[]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")

# ---- eq direction: the stored element is the receiver ----
probe_log = []


class P:
    def __init__(self, v, raiser=False):
        self.v = v
        self.raiser = raiser

    def __hash__(self):
        return 1000

    def __eq__(self, o):
        probe_log.append((self.v, getattr(o, "v", "?")))
        if self.raiser:
            raise RuntimeError("eq-boom")
        return isinstance(o, P) and o.v == self.v


# insert with colliding hashes compares via __eq__ (stored as receiver)
a, b = P(1), P(2)
pair = {a, b}
assert len(pair) == 2
assert probe_log and probe_log[0] == (1, 2)
del probe_log[:]

# identity shortcut: x in {x} never calls __eq__
identity = P(9)
solo = {identity}
assert identity in solo
assert probe_log == []

# asymmetric __eq__ resolves through the reflected call
class A:
    def __hash__(self):
        return 555

    def __eq__(self, o):
        return NotImplemented


class B:
    def __hash__(self):
        return 555

    def __eq__(self, o):
        return isinstance(o, A)


assert B() in {A()}
assert A() in {B()}

# issubset with a non-set iterable deduplicates hits (CPython builds the
# intersection first)
assert not {1, 2}.issubset([1, 1])
assert {1}.issubset([1, 1])
assert not {1, 2}.issubset((1, 1, 1))
assert {1, 2}.issubset((1, 2, 1))

# isdisjoint with itself short-circuits on the size
selfset = {1, 2}
assert not selfset.isdisjoint(selfset)
assert set().isdisjoint(set())

# a raising stored __eq__ surfaces as a catchable RuntimeError; a raising
# searched key only raises when a stored candidate is actually compared
boom = P(7, raiser=True)
plain = {P(1)}
boom_set = {boom}

assert boom not in plain  # stored receivers are P(1)/P(2)-like, no raise
expect("in-stored", lambda: P(1) in boom_set, RuntimeError, "eq-boom")
expect("le-stored", lambda: plain <= boom_set, RuntimeError, "eq-boom")
expect("sub-stored", lambda: plain - boom_set, RuntimeError, "eq-boom")
expect("or-stored", lambda: boom_set | plain, RuntimeError, "eq-boom")
expect("and-stored", lambda: boom_set & plain, RuntimeError, "eq-boom")
expect("eq-stored", lambda: plain == boom_set, RuntimeError, "eq-boom")
expect("issubset-stored", lambda: plain.issubset(boom_set), RuntimeError, "eq-boom")
expect("isdisjoint-stored", lambda: boom_set.isdisjoint(plain), RuntimeError, "eq-boom")
expect("remove-stored", lambda: boom_set.remove(P(1)), RuntimeError, "eq-boom")
expect("discard-stored", lambda: boom_set.discard(P(1)), RuntimeError, "eq-boom")
expect("fs-in-stored", lambda: P(1) in frozenset([boom]), RuntimeError, "eq-boom")

# ---- set iterator semantics (CPython si_used snapshot) ----
it = iter({1, 2, 3})
assert it.__length_hint__() == 3
assert next(it) in {1, 2, 3}
assert it.__length_hint__() == 2

# the hint reads 0 once a size change invalidates the iterator or after
# exhaustion (CPython setiter_len)
hint = {1, 2, 3}
hi = iter(hint)
hint.add(4)
assert hi.__length_hint__() == 0
hi2 = iter(hint)
while True:
    try:
        next(hi2)
    except StopIteration:
        break
assert hi2.__length_hint__() == 0

grow = {1, 2, 3}
gi = iter(grow)
grow.add(99)
expect("grow", lambda: next(gi), RuntimeError, "Set changed size during iteration")
expect("sticky", lambda: next(gi), RuntimeError, "Set changed size during iteration")

shrink = {1, 2, 3}
si = iter(shrink)
shrink.discard(1)
expect("shrink", lambda: next(si), RuntimeError, "Set changed size during iteration")

# same-size mutation does not disturb the iterator (used-count check)
same = {1, 2, 3}
visited = []
for q in same:
    visited.append(q)
    same.discard(2)
    same.add(5)
assert sorted(visited) == [1, 3, 5]
assert sorted(same) == [1, 3, 5]

exhausted = {1, 2, 3}
ei = iter(exhausted)
while True:
    try:
        next(ei)
    except StopIteration:
        break
exhausted.add(100)
try:
    next(ei)
    assert False, "exhausted iterator must stop"
except StopIteration:
    pass

consume = {1, 2, 3}
raised = False
try:
    for q in consume:
        consume.add(q * 10)
except RuntimeError as e:
    raised = str(e) == "Set changed size during iteration"
assert raised

# ---- frozenset faces ----
f = frozenset([H(1), H(2)])
g = {H(1)}
assert H(1) in f and H(3) not in f
assert sorted(q.v for q in (f - g)) == [2]
assert len(f | g) == 2 and len(f & g) == 1 and len(f ^ g) == 1
assert f == frozenset([H(1), H(2)])
assert f != {H(1)}
assert f.copy() is f
assert frozenset(f) is f
assert len(frozenset(g)) == 1
assert type(set(f)).__name__ == "set"
assert hash(f) == hash(frozenset([H(1), H(2)]))
assert f.issubset(frozenset([H(1), H(2), H(3)]))
assert f.isdisjoint({H(9)})
expect("hash-fs", lambda: hash({1}), TypeError, "unhashable type: 'set'")

# the cached frozenset hash settles unequal pairs without element equality
class Q:
    def __init__(self, v):
        self.v = v

    def __hash__(self):
        return self.v

    def __eq__(self, o):
        probe_log.append(("eq", self.v, getattr(o, "v", "?")))
        return isinstance(o, Q) and o.v == self.v


fh = frozenset([Q(1)])
gh = frozenset([Q(2)])
hash(fh)
hash(gh)
del probe_log[:]
assert not (fh == gh)
assert fh != gh
assert probe_log == [], "cached hash fast-fail leaked into eq"

# ---- set keys fall back to the frozenset hash on lookup (CPython
# set_contains_lock_held/set_remove_impl/set_discard_impl), add never does ----
assert set() in {frozenset()}
fs_store = {frozenset({1})}
fs_store.discard({1})
assert fs_store == set()
fs_store2 = {frozenset({2})}
expect("remove-set-key-missing", lambda: fs_store2.remove({9}), KeyError, "{9}")
expect("add-no-fallback", lambda: {frozenset()} | {set()}, TypeError,
       "cannot use 'set' as a set element (unhashable type: 'set')")

# only an exact TypeError is re-wrapped; subclasses keep their identity
class SubTypeError(TypeError):
    pass


class SubRaise:
    def __eq__(self, o):
        return False

    def __hash__(self):
        raise SubTypeError("sub")


try:
    SubRaise() in {1}
    assert False, "no SubTypeError"
except SubTypeError as e:
    assert str(e) == "sub"

# a set subclass whose __hash__ raises a non-TypeError propagates it raw
# on lookup (the frozenset retry only engages for TypeError)
class BadHash(set):
    def __hash__(self):
        raise ValueError("vh")


expect("set-key-non-typeerror", lambda: BadHash() in {frozenset({1})}, ValueError, "vh")

# frozenset hashing xors the STORED element hashes: no second __hash__ call
fresh = Counting()
frozen_counting = frozenset([fresh])
first = hash(frozen_counting)
second = hash(frozen_counting)
assert first == second and fresh.n == 1

# a frozenset subclass __init__ consumes keyword arguments
class FS(frozenset):
    def __init__(self, it, **kw):
        self.kw = kw


fs_sub = FS({1}, x=2)
assert len(fs_sub) == 1 and type(fs_sub).__name__ == "FS"

# ---- stored-hash fidelity across table copies ----
big = {H(i) for i in range(64)}
copied = big.copy()
assert len(copied) == 64
assert all(q in copied for q in big)

print("test_set_user_hash_eq_regression passed")
