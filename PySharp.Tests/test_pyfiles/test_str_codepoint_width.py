"""
zfill and %-formatting measure width and precision in
code points, not in UTF-16 code units, so an astral character occupies a
single column.

CPython 3.14 reference values are inline in the assertions.

:kind: test
"""

astral = chr(0x1F600)
s = 'a' + astral + 'b'

# zfill counts code points and inserts after a leading sign
assert len(('-' + astral).zfill(4)) == 4
assert ('-' + astral).zfill(4) == '-00' + astral
assert (astral + 'ab').zfill(5) == '00' + astral + 'ab'
assert astral.zfill(2) == '0' + astral
assert astral.zfill(1) == astral
assert '42'.zfill(5) == '00042'
assert '-42'.zfill(5) == '-0042'

# %-formatting width
assert '%5s' % astral == '    ' + astral
assert '%-5s|' % astral == astral + '    |'
assert '%*s' % (5, astral) == '    ' + astral
assert '%5c' % 0x1F600 == '    ' + astral
assert len('%6s|' % s) == 7
assert '%6s|' % s == '   ' + s + '|'

# %-formatting precision truncates to whole code points
assert '%.2s' % s == 'a' + astral
assert '%.*s' % (2, s) == 'a' + astral
assert '%.0s' % s == ''
assert '%.9s' % 'abc' == 'abc'
assert '%.3s' % 'abcdef' == 'abc'
assert '%.3r' % s == "'a\U0001F600"

print("test_str_codepoint_width passed")
