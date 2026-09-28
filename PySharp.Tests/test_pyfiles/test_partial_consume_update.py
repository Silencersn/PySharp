"""list.extend/+= 与 dict.update 逐项提交：源在中途抛错时，已消费的项留在目标容器里。

:kind: test
"""

# CPython writes into the target container inside the consume loop
# (listobject.c list_extend_iter_lock_held appends per item; dictobject.c
# dict_merge / PyDict_MergeFromSeq2 call setitem per key), so a source that
# raises partway leaves the items it already produced visible on the target.
# Materializing the source first and committing at the end would instead
# leave the target untouched — the behaviour this pins against.
#
# set.update was already incremental and stays the reverse control: the same
# family of mutable containers is expected to agree here.

def expect(label, fn, exc):
    try:
        fn()
    except exc:
        return
    except BaseException as e:
        assert False, f"{label}: {type(e).__name__}({e}) is not {exc.__name__}"
    assert False, f"{label}: no {exc.__name__}"


def bad_seq():
    yield 1
    yield 2
    raise ValueError("mid")


# --- list.extend keeps what the iterator already yielded ---
lst = [0]
try:
    lst.extend(bad_seq())
except ValueError as e:
    assert str(e) == "mid", str(e)
assert lst == [0, 1, 2], lst

# --- += goes through the same slot ---
lst2 = [0]
try:
    lst2 += bad_seq()
except ValueError as e:
    assert str(e) == "mid", str(e)
assert lst2 == [0, 1, 2], lst2

# a generator that yields nothing before failing leaves the list alone
def immediately_bad():
    raise ValueError("first")
    yield

lst3 = [7]
try:
    lst3.extend(immediately_bad())
except ValueError as e:
    assert str(e) == "first", str(e)
assert lst3 == [7], lst3

# --- dict.update: source raises after two pairs ---
d = {0: "a"}


def bad_pairs():
    yield (1, "b")
    yield (2, "c")
    raise ValueError("mid2")


try:
    d.update(bad_pairs())
except ValueError as e:
    assert str(e) == "mid2", str(e)
assert d == {0: "a", 1: "b", 2: "c"}, d

# --- dict.update: element shape illegal on the second pair ---
d2 = {}


def bad_shape():
    yield (0, "a")
    yield 42


# the message text is a separate divergence; only the type and the committed
# prefix are pinned here
expect("shape", lambda: d2.update(bad_shape()), TypeError)
assert d2 == {0: "a"}, d2

# --- dict.update: unhashable key on the second pair ---
d3 = {}


def bad_key():
    yield (1, "a")
    yield ([1], "b")


try:
    d3.update(bad_key())
except TypeError as e:
    assert str(e) == "cannot use 'list' as a dict key (unhashable type: 'list')", str(e)
assert d3 == {1: "a"}, d3

# --- dict.update: mapping-like source whose __getitem__ fails on key 1 ---
class MidwayMapping:
    def keys(self):
        return [0, 1]

    def __getitem__(self, k):
        if k == 0:
            return "v0"
        raise ValueError("mm")


d4 = {}
try:
    d4.update(MidwayMapping())
except ValueError as e:
    assert str(e) == "mm", str(e)
assert d4 == {0: "v0"}, d4

# --- reverse control: set.update was already incremental and still is ---
s = {0}


def bad_set():
    yield 1
    raise ValueError("sb")


try:
    s.update(bad_set())
except ValueError as e:
    assert str(e) == "sb", str(e)
assert s == {0, 1}, s

# --- controls: a source that fails before yielding anything ---
l5 = [0]
try:
    l5.extend(5)
except TypeError as e:
    assert str(e) == "'int' object is not iterable", str(e)
assert l5 == [0], l5

d5 = {0: "a"}
try:
    d5.update(5)
except TypeError:
    pass
assert d5 == {0: "a"}, d5

# --- a plain dict source, and self-source on both containers ---
d6 = {"x": 1}
d6.update({"y": 2})
assert d6 == {"x": 1, "y": 2}, d6

a = [1, 2]
a.extend(a)
assert a == [1, 2, 1, 2], a

b = [1, 2]
b += b
assert b == [1, 2, 1, 2], b

d7 = {1: 2}
d7.update(d7)
assert d7 == {1: 2}, d7

# --- a failing keys() leaves the target empty ---
class BadKeys:
    def keys(self):
        raise RuntimeError("kb")


d8 = {}
try:
    d8.update(BadKeys())
except RuntimeError as e:
    assert str(e) == "kb", str(e)
assert d8 == {}, d8

# --- the successful paths are unchanged ---
ok = [0]
ok.extend([1, 2])
assert ok == [0, 1, 2], ok

ok2 = {0: "a"}
ok2.update([(1, "b"), (2, "c")])
assert ok2 == {0: "a", 1: "b", 2: "c"}, ok2

ok3 = {}
ok3.update([("k", "v")], other="o")
assert ok3 == {"k": "v", "other": "o"}, ok3

print("test_partial_consume_update passed")
