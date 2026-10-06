"""str find/count/partition/expandtabs match CPython's edge-case contracts.

partition/rpartition miss falls back to (self, '', '') / ('', '', self), an
empty needle counts and finds at the zero-width window with clamped bounds,
and expandtabs treats a negative tabsize as 0.

:kind: test
"""

# str find/count/partition/expandtabs, aligned with CPython:
#
# - partition falls back to (self, '', '') when the separator is absent
#   (rpartition's miss yields ('', '', self))
# - ADJUST_INDICES clamps end into [0, len] but start only at 0, so an
#   above-range start keeps end - start negative (the miss signal)
# - an empty needle counts/finds at the zero-width window: count is
#   (end - start) + 1 for a valid window and 0 for a negative one, find
#   reports the window start, rfind the window end
# - expandtabs accepts a negative tabsize as 0 (tabs deleted, no error)

assert 'abc'.partition('z') == ('abc', '', '')

assert 'abcd'.partition('cd') == ('ab', 'cd', '')
assert 'abc'.rpartition('z') == ('', '', 'abc')
assert 'abzcz'.partition('z') == ('ab', 'z', 'cz')

# empty-needle count matrix
assert ''.count('') == 1
assert ''.count('', 0, 0) == 1
assert 'abc'.count('', 1, 1) == 1
assert ''.count('', 1, 2) == 0
assert 'ab'.count('', 0, 5) == 3
assert 'ab'.count('', -1) == 2
assert 'abc'.count('') == 4
assert 'abc'.count('', 3) == 1
assert 'abc'.count('', 2, 1) == 0
assert 'abc'.count('', -1, -1) == 1
assert 'abc'.count('', 1, 0) == 0
assert 'abc'.count('', 0, 99) == 4
assert 'abc'.count('', -99) == 4
assert ''.count('', -1) == 1
assert 'ab'.count('', 1, 1) == 1
assert 'ab'.count('', 2) == 1
assert 'ab'.count('', 3) == 0
assert 'ab'.count('', 2, 2) == 1
assert 'ab'.count('b', 5) == 0

# empty-needle find/rfind/index matrix
assert "".find("") == 0
assert "".rfind("") == 0
assert "".index("") == 0
assert "abc".find("", 1, 1) == 1
assert "abc".find("", 2, 1) == -1
assert "abc".find("", 5) == -1
assert "abc".find("", 3) == 3
assert "abc".find("", -1) == 2
assert "abc".rfind("", 1, 1) == 1
assert "abc".rfind("", 2, 1) == -1
assert "abc".rfind("", 5) == -1
assert "abc".find("", 0, 0) == 0
assert "".find("", 1, 2) == -1
assert "".find("", 0, 1) == 0
assert "abc".rfind("") == 3
assert "abc".rfind("", 0, 0) == 0
assert "abc".rindex("") == 3
try:
    "abc".index("", 5)
except ValueError as e:
    assert str(e) == "substring not found", str(e)
else:
    raise AssertionError("index must raise past the end")

# non-empty needles keep the clamped behaviour
assert 'abc'.find('b', 5) == -1
assert 'abc'.count('b', 5) == 0
assert '😀😀'.count('') == 3
assert '😀😀'.find('', 1) == 1

# expandtabs negative tabsize
assert 'a\tb'.expandtabs(-1) == 'ab'
assert 'a\tb'.expandtabs(-100) == 'ab'
assert 'a\tb'.expandtabs(0) == 'ab'
assert 'a\tb'.expandtabs(1) == 'a b'
assert 'a\r\tb'.expandtabs(2) == 'a\r  b'
assert 'a\tb'.expandtabs() == 'a       b'

# astral content walks the surrogate-aware index paths: offsets and results
# are in code points whether or not the fast identity-indexing applies
a = "x😀y😀z" * 500
assert (a.find("😀"), a.find("😀", 2), a.find("😀", 4), a.find("q")) == (1, 3, 6, -1)
assert (a.rfind("😀"), a.rfind("z"), a.rfind("x", 1), a.rfind("q")) == (2498, 2499, 2495, -1)
assert (a.count("😀"), a.count("😀", 0, 4), a.count("x😀"), a.count("y😀z", 3), a.count("q")) == (1000, 2, 500, 499, 0)
assert (a.index("😀", 5), a.rindex("z")) == (6, 2499)
# the needle must fit entirely inside [start, end)
assert (a.find("😀", 0, 2), a.find("😀", 0, 3), a.count("😀", 0, 2), a.count("😀", 0, 3)) == (1, 1, 1, 1)
assert (a.startswith("😀", 1, 2), a.startswith("😀", 1, 3), a.endswith("y", 0, 3), a.endswith("y", 0, 2)) == (True, True, True, False)
assert a.startswith("x😀") and a.startswith("😀", 1) and a.endswith("z") and a.endswith("😀z")

# a lone surrogate is a width-1 code point of its own
b = "ab\uDD00cd"
assert (b.find("\uDD00"), b.count("\uDD00"), b.rfind("\uDD00")) == (2, 1, 2)
assert not b.startswith("a\uDD00") and b.endswith("cd")
c = "\U0001F600\uD800x"
assert (c.find("\uD800"), c.find("\U0001F600", 1), c.count("\U0001F600"), c.rfind("x"), len(c)) == (1, -1, 1, 2, 3)

# window arithmetic on plain BMP content
d = "aaaa"
assert (d.find("aa"), d.count("aa"), d.rfind("aa"), d.find("aa", 1, 3), d.count("aa", 1, 3)) == (0, 2, 2, 1, 1)
assert (d.startswith("aa", 2), d.endswith("aa", 0, 3)) == (True, True)

# a single astral code point exactly fills a one-code-point window
f = "😀"
assert (f.find("😀", 0, 1), f.rfind("😀", 0, 1), f.count("😀", 0, 1)) == (0, 0, 1)
assert (f.startswith("😀", 0, 1), f.endswith("😀", 0, 1)) == (True, True)
assert (f.find("😀", 0, 0), f.count("x😀")) == (-1, 0)

# large input keeps the offset contracts (regression guard for the
# quadratic window-copy path)
g = "abc" * 30000
assert (g.find("abc", 29999), g.rfind("abc", 0, 2), g.count("abc", 29990)) == (30000, -1, 20003)
assert (g.startswith("abc", 29998), g.endswith("c", 0, 89999), g.startswith("abc", 29999), g.endswith("abc", 0, 90000)) == (False, False, False, True)

print("ok")
