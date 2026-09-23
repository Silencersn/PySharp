# container repr vs mutation regression, aligned with CPython:
#
# - set repr snapshots its keys before rendering (set_repr_lock_held,
#   gh-129967): elements added by an element's __repr__ do NOT appear
#   in the text (len() still shows them)
# - dict repr live-iterates (dict_repr_lock_held, "Note that repr may
#   mutate the dict"): entries inserted by a key/value __repr__ DO
#   appear, appended after the entries rendered so far
# - list repr re-fetches the size each step ("Note that this may mutate
#   the list"): elements appended during repr are rendered too, and the
#   mutation must not crash the interpreter

class SetMut:
    def __init__(self, v, mutate=None):
        self.v = v
        self.mutate = mutate
    def __hash__(self):
        return hash(self.v)
    def __eq__(self, other):
        return isinstance(other, SetMut) and self.v == other.v
    def __repr__(self):
        if self.mutate is not None:
            self.mutate.add(SetMut(99))
        return "M(%d)" % self.v

s = set()
s.add(SetMut(1))
s.add(SetMut(2, s))
assert repr(s) == "{M(1), M(2)}", repr(s)
assert len(s) == 3
print("set snapshot ok")

class DictMut:
    def __init__(self, v, mutate=None):
        self.v = v
        self.mutate = mutate
    def __hash__(self):
        return hash(self.v)
    def __eq__(self, other):
        return isinstance(other, DictMut) and other.v == self.v
    def __repr__(self):
        if self.mutate is not None:
            self.mutate["added"] = 1
        return "M(%d)" % self.v

d = {}
d[DictMut(1)] = DictMut(2, d)
assert repr(d) == "{M(1): M(2), 'added': 1}", repr(d)
assert len(d) == 2
print("dict value-side live ok")

# insertion order: the new entry lands after everything rendered so far
d2 = {}
d2[DictMut(1)] = DictMut(2, d2)
d2["plain"] = "x"
assert repr(d2) == "{M(1): M(2), 'plain': 'x', 'added': 1}", repr(d2)
assert len(d2) == 3
print("dict ordering ok")

class KeyMut:
    def __init__(self, v, d=None):
        self.v = v
        self.d = d
    def __hash__(self):
        return hash(self.v)
    def __eq__(self, other):
        return isinstance(other, KeyMut) and other.v == self.v
    def __repr__(self):
        if self.d is not None:
            self.d["k"] = 1
        return "K(%d)" % self.v

d3 = {}
d3[KeyMut(1, d3)] = 2
assert repr(d3) == "{K(1): 2, 'k': 1}", repr(d3)
assert len(d3) == 2
print("dict key-side live ok")

class ListMut:
    def __init__(self, l, mutate=None):
        self.l = l
        self.mutate = mutate
    def __repr__(self):
        if self.mutate is not None:
            self.mutate.append(ListMut(None))
        return "L"

lst = []
lst.append(ListMut(None))
lst.append(ListMut(lst, lst))
assert repr(lst) == "[L, L, L]", repr(lst)
assert len(lst) == 3
print("list live ok")

print("all ok")
