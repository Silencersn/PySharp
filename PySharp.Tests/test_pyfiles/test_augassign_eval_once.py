"""
Regression test: augmented assignment must evaluate the target's sub-expressions
exactly ONCE (CPython 3.14 semantics via Copy/Swap on the stack).

The old emitter re-emitted the target for the store, which re-evaluated the
container/key (a[b] += c) or the object (o.attr += c) and duplicated side effects.

Covers:
- a[b] += c  : container and key evaluated once; __getitem__/__setitem__ hit the same object
- o.attr += c: object evaluated once
- slice targets: a[i:j] += c
- name targets: x += c (unchanged path)
- plain assignment a[b] = c still evaluates each part once
"""

# ============================================================
# subscript: a()[b()] += c()  -> a, b, c each evaluated once
# ============================================================
calls = {"a": 0, "b": 0, "c": 0}


class SubBox:
    def __init__(self):
        self.d = {"k": 0}

    def __getitem__(self, key):
        return self.d[key]

    def __setitem__(self, key, value):
        self.d[key] = value


def make_a():
    calls["a"] += 1
    return SubBox()


def make_b():
    calls["b"] += 1
    return "k"


def make_c():
    calls["c"] += 1
    return 10


make_a()[make_b()] += make_c()
assert calls == {"a": 1, "b": 1, "c": 1}, calls


# container factory: __getitem__ and __setitem__ must hit the SAME object
class SubFactory:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return SubBox()


f = SubFactory()
f()["k"] += 1
assert f.n == 1, f.n

# result correctness
box = SubBox()
box["k"] += 1
assert box.d["k"] == 1

# ============================================================
# attribute: o().attr += c()  -> o evaluated once
# ============================================================
calls = {"o": 0, "c": 0}


class AttrBox:
    def __init__(self):
        self.attr = 0


def make_o():
    calls["o"] += 1
    return AttrBox()


def make_v():
    calls["c"] += 1
    return 5


make_o().attr += make_v()
assert calls == {"o": 1, "c": 1}, calls


class AttrFactory:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return AttrBox()


f = AttrFactory()
f().attr += 5
assert f.n == 1, f.n

# ============================================================
# slice target: a()[i:j] += c  -> container evaluated once
# ============================================================
class ListBox:
    def __init__(self):
        self.data = [1, 2, 3]

    def __getitem__(self, key):
        return self.data[key]

    def __setitem__(self, key, value):
        self.data[key] = value


class ListFactory:
    def __init__(self):
        self.n = 0

    def __call__(self):
        self.n += 1
        return ListBox()


f = ListFactory()
f()[1:2] += [9]
assert f.n == 1, f.n

# ============================================================
# name target: x += c()  (unchanged path, value evaluated once)
# ============================================================
x = 1
calls = {"v": 0}


def make_val():
    calls["v"] += 1
    return 2


x += make_val()
assert x == 3
assert calls == {"v": 1}, calls

# ============================================================
# plain assignment a[b] = c still evaluates each part once
# ============================================================
calls = {"a": 0, "b": 0, "c": 0}
make_a()[make_b()] = make_c()
assert calls == {"a": 1, "b": 1, "c": 1}, calls

print("test_augassign_eval_once passed")
