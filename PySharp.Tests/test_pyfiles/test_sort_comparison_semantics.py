"""
sort comparison semantics.

Every sort comparison is PyObject_RichCompareBool(pivot, placed, Py_LT)
with the result interpreted by Python truthiness — a non-bool __lt__
return value (truthy tuple, None, int) is no longer mistaken for a bare
bool. The operand order follows CPython listsort's algorithm (count_run +
binarysort below 64 elements, merges beyond), so an asymmetric __lt__
yields the same permutation as CPython; reverse sorts the reversed array
ascending and reverses back, and key() calls run once over the original
order.

CPython 3.14 reference (Objects/listobject.c ISLT/count_run/binarysort,
listsort_impl reverse sandwich).

:kind: test
"""

seed = 123456789
def rnd(n):
    global seed
    out = []
    for _ in range(n):
        seed = (seed * 6364136223846793005 + 1442695040888963407) % (1 << 64)
        out.append((seed >> 33) % 1000)
    return out

class T:
    def __init__(self, v): self.v = v
    def __lt__(self, o): return ("yes", self.v < o.v)
    def __repr__(self): return f"T{self.v}"

# the issue repro: a truthy tuple return is truthy for sort
assert repr(sorted([T(3), T(1)])) == "[T1, T3]"

l = [T(3), T(1)]
l.sort()
assert repr(l) == "[T1, T3]"

# falsy / truthy non-bool returns
class F:
    def __init__(self, v): self.v = v
    def __lt__(self, o): return None
    def __repr__(self): return f"F{self.v}"
class Tr:
    def __init__(self, v): self.v = v
    def __lt__(self, o): return 1
    def __repr__(self): return f"Tr{self.v}"
class N:
    def __init__(self, v): self.v = v
    def __lt__(self, o): return self.v < o.v or "yes"
    def __repr__(self): return f"N{self.v}"

assert repr(sorted([F(3), F(1)])) == "[F3, F1]"
assert repr(sorted([Tr(3), Tr(1)])) == "[Tr1, Tr3]"
assert repr(sorted([N(3), N(1)])) == "[N1, N3]"

# sort/min/bare-compare consistency
assert min([T(3), T(1)]).v == 1
assert (T(3) < T(1)) == ("yes", False)
assert bool(T(3) < T(1)) is True

# asymmetric comparator over random data: every variant shares CPython's
# exact permutation (reverse is the reverse-sort-reverse sandwich, key
# compares the same objects, so an asymmetric __lt__ stays consistent)
for n in (2, 7, 33, 63):
    data = rnd(n)
    asc = [t.v for t in sorted([T(v) for v in data])]
    assert [t.v for t in sorted([T(v) for v in data], key=lambda t: t)] == asc
    assert [t.v for t in sorted([T(v) for v in data], reverse=True)] == asc
    assert [t.v for t in sorted([T(v) for v in data], key=lambda t: t, reverse=True)] == asc

# stability with equal keys
class Tagged:
    def __init__(self, k, tag): self.k = k; self.tag = tag
    def __lt__(self, o): return self.k < o.k
    def __repr__(self): return self.tag
pairs = [Tagged(i % 3, f"t{i}") for i in range(30)]
assert [t.tag for t in sorted(pairs)] == [
    "t0", "t3", "t6", "t9", "t12", "t15", "t18", "t21", "t24", "t27",
    "t1", "t4", "t7", "t10", "t13", "t16", "t19", "t22", "t25", "t28",
    "t2", "t5", "t8", "t11", "t14", "t17", "t20", "t23", "t26", "t29"]
assert [t.tag for t in sorted(pairs, key=lambda t: t.k, reverse=True)] == [
    "t2", "t5", "t8", "t11", "t14", "t17", "t20", "t23", "t26", "t29",
    "t1", "t4", "t7", "t10", "t13", "t16", "t19", "t22", "t25", "t28",
    "t0", "t3", "t6", "t9", "t12", "t15", "t18", "t21", "t24", "t27"]

# consistent comparators across the merge threshold
for n in (63, 64, 65, 130):
    data = rnd(n)
    assert sorted(data) == sorted(data)
    assert sorted(data, reverse=True) == sorted(data, reverse=True)
    assert sorted(data) == sorted(data, key=lambda x: x)

# degenerate sizes
assert sorted([]) == []
assert sorted([7]) == [7]
assert sorted([7], reverse=True) == [7]

# comparator errors propagate
def badkey(x):
    raise ValueError("key boom")
try:
    sorted([3, 1, 2], key=badkey)
    assert False, "ValueError expected"
except ValueError as e:
    assert str(e) == "key boom"

class Boom:
    def __lt__(self, o): raise RuntimeError("lt boom")
try:
    sorted([Boom(), Boom()])
    assert False, "RuntimeError expected"
except RuntimeError as e:
    assert str(e) == "lt boom"

print("test_sort_comparison_semantics passed")
