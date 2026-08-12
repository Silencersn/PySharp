"""
Regression: str.strip/lstrip/rstrip with an empty 'chars' argument must not
strip anything (CPython: an empty chars set means "strip nothing").

CPython 3.14 reference:
    '  abc  '.strip('')   -> '  abc  '
    '  abc  '.lstrip('')  -> '  abc  '
    '  abc  '.rstrip('')  -> '  abc  '
    '   '.strip('')       -> '   '
    '\t \n'.strip('')     -> '\t \n'

Previously PySharp passed the empty chars string straight to .NET
Trim(char[])/TrimStart/TrimEnd, where an empty char array is equivalent to
Trim() (strip all whitespace), so the empty-chars boundary stripped
whitespace instead of leaving the string untouched.
"""

s = '  abc  '
assert s.strip('') == '  abc  '
assert s.lstrip('') == '  abc  '
assert s.rstrip('') == '  abc  '

assert '   '.strip('') == '   '
assert '\t \n'.strip('') == '\t \n'

# None (default) must keep trimming whitespace
assert s.strip() == 'abc'
assert s.lstrip() == 'abc  '
assert s.rstrip() == '  abc'

# non-empty chars still work
assert 'xxabcxx'.strip('x') == 'abc'
assert 'xxabcxx'.lstrip('x') == 'abcxx'
assert 'xxabcxx'.rstrip('x') == 'xxabc'

print("test_str_strip_empty_chars_regression passed")
