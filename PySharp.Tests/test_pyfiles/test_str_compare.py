"""
str comparison must use ordinal (code point) ordering and
support <= / >=.

CPython 3.14 reference:
    'a' < 'B'  -> False   ('a' = 97 > 'B' = 66)
    'a' > 'B'  -> True
    'a' <= 'B' -> False
    'a' >= 'B' -> True
    sorted(['B','a','A','b']) -> ['A', 'B', 'a', 'b']

:kind: test
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
# Lone surrogates compare by their own code point
assert ('\ud800' < '\udc00') is True

# Astral characters sort above the whole BMP: comparing UTF-16 code units
# instead would place the high surrogate (U+D800-U+DBFF) below U+E000-U+FFFF
assert (chr(0xFFFF) < chr(0x10000)) is True
assert (chr(0xE000) < chr(0x10000)) is True
assert (chr(0xD7FF) < chr(0x10000)) is True
assert (chr(0x10000) > chr(0xFFFF)) is True
assert (chr(0x10000) < chr(0x10FFFF)) is True
assert ('a' + chr(0x10000)) > ('a' + chr(0xFFFF))
assert min(chr(0x10000), chr(0xFFFF)) == chr(0xFFFF)
assert max(chr(0x10000), chr(0xFFFF)) == chr(0x10000)
assert sorted([chr(0x10000), chr(0xFFFF), 'a']) == ['a', chr(0xFFFF), chr(0x10000)]

# sorted uses the same ordering
assert sorted(['B', 'a', 'A', 'b']) == ['A', 'B', 'a', 'b']

print("test_str_compare passed")
