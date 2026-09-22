# str find/count/partition/expandtabs regression, aligned with CPython:
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

print("ok")
