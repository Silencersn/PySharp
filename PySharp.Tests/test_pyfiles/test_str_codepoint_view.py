"""
the str character views (index, slice, iterate, pad,
replace) count code points, so an astral character counts once and an
unpaired surrogate stays itself instead of becoming U+FFFD.

CPython 3.14 reference values are inline in the assertions.

:kind: test
"""

astral = chr(0x1F600)
hi = '\ud800'

# indexing and slicing
s = hi + 'x' + astral
assert len(s) == 3
assert ord(s[0]) == 0xD800
assert s[1] == 'x'
assert ord(s[2]) == 0x1F600
assert [ord(c) for c in s[1:3]] == [0x78, 0x1F600]
assert [ord(c) for c in s[::-1]] == [0x1F600, 0x78, 0xD800]
assert [ord(c) for c in s[::2]] == [0xD800, 0x1F600]
assert ord(s[-1]) == 0x1F600

# iteration
assert [ord(c) for c in s] == [0xD800, 0x78, 0x1F600]
assert [ord(c) for c in list(s)] == [0xD800, 0x78, 0x1F600]
assert [ord(c) for c in iter(s)] == [0xD800, 0x78, 0x1F600]
assert ''.join(s) == s

# rebuilding methods keep the surrogate
assert s.replace('x', 'y') == hi + 'y' + astral
assert s.find('x') == 1
assert (hi + 'AB').capitalize() == hi + 'ab'
assert 'ab'.replace('', '-') == '-a-b-'

# repr/ascii escape a lone surrogate rather than emit U+FFFD
assert repr(hi) == "'\\ud800'"
assert repr('a' + hi) == "'a\\ud800'"
assert '\ufffd' not in repr(hi)
assert repr('\U0001F600') == "'\U0001F600'"

# an astral fill character survives padding
assert 'a'.rjust(3, astral) == astral + astral + 'a'
assert 'a'.ljust(3, astral) == 'a' + astral + astral
assert [ord(c) for c in 'a'.rjust(2, astral)] == [0x1F600, 0x61]
assert len('a'.center(4, astral)) == 4
assert [ord(c) for c in 'a'.rjust(3, hi)] == [0xD800, 0xD800, 0x61]
assert 'a'.rjust(3, 'é') == 'ééa'

print("test_str_codepoint_view passed")
