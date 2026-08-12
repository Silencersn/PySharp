"""
Regression: str.replace('', new, count) must follow CPython's interleave
semantics (n = min(count, len(s)+1) insertions) and must not leak a raw
.NET exception for the default count.

CPython 3.14 reference:
    'abc'.replace('', 'x')    -> 'xaxbxcx'
    'abc'.replace('', 'x', 0) -> 'abc'
    'abc'.replace('', 'x', 1) -> 'xabc'
    'abc'.replace('', 'x', 2) -> 'xaxbc'
    'abc'.replace('', 'x', 3) -> 'xaxbxc'
    'abc'.replace('', 'x', 9) -> 'xaxbxcx'   (count capped at len+1)

Previously the default count hit .NET string.Replace with an empty oldValue,
which throws a raw System.ArgumentException (bypassing Python try/except and
crashing the interpreter); explicit counts inserted one extra time
(off-by-one: count=0 still inserted once).
"""

assert 'abc'.replace('', 'x') == 'xaxbxcx'
assert 'abc'.replace('', 'x', 0) == 'abc'
assert 'abc'.replace('', 'x', 1) == 'xabc'
assert 'abc'.replace('', 'x', 2) == 'xaxbc'
assert 'abc'.replace('', 'x', 3) == 'xaxbxc'
assert 'abc'.replace('', 'x', 9) == 'xaxbxcx'

# empty new / empty subject edges
assert 'abc'.replace('', '') == 'abc'
assert ''.replace('', 'x') == 'x'
assert ''.replace('', 'x', 0) == ''

# non-empty old is unaffected
assert 'abab'.replace('a', 'X') == 'XbXb'
assert 'abab'.replace('a', 'X', 1) == 'Xbab'

print("test_str_replace_empty_old_regression passed")
