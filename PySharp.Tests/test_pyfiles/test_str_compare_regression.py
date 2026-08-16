"""
Regression: str comparison must use ordinal (code point) ordering and
support <= / >=.

CPython 3.14 reference:
    'a' < 'B'  -> False   ('a' = 97 > 'B' = 66)
    'a' > 'B'  -> True
    'a' <= 'B' -> False
    'a' >= 'B' -> True
    sorted(['B','a','A','b']) -> ['A', 'B', 'a', 'b']
"""

# Ordinal (code point) ordering, not culture ordering
assert ('a' < 'B') is False
assert ('a' > 'B') is True
assert ('a' <= 'B') is False
assert ('a' >= 'B') is True
assert ('B' < 'a') is True
assert ('B' <= 'a') is True
assert ('B' > 'a') is False
assert ('B' >= 'a') is False

# Equal strings
assert ('abc' <= 'abc') is True
assert ('abc' >= 'abc') is True
assert ('abc' < 'abc') is False
assert ('abc' > 'abc') is False

# Prefix ordering
assert ('ab' < 'abc') is True
assert ('abc' > 'ab') is True

# Code point order beyond ASCII (accented chars, non-BMP, lone surrogates)
assert ('é' > 'e') is True
assert ('😀' > '中') is True
# Lone surrogates via literal (chr() rejects the surrogate range)
assert ('\ud800' < '\udc00') is True

# sorted uses the same ordering
assert sorted(['B', 'a', 'A', 'b']) == ['A', 'B', 'a', 'b']

print("test_str_compare_regression passed")
