"""
the str predicates follow CPython's Unicode tables rather
than the general-category approximations — Numeric_Type=Digit/Numeric,
the explicit space list, and the derived Other_Uppercase/Other_Lowercase
properties.

CPython 3.14 reference values are inline in the assertions.

:kind: test
"""

# isdigit: Numeric_Type=Digit|Decimal, isdecimal: Nd only
assert '²'.isdigit() is True
assert '²'.isdecimal() is False
assert '²'.isnumeric() is True
assert '²'.isalnum() is True
assert '²'.isalpha() is False
assert '٣'.isdigit() is True          # U+0663, Nd
assert '٣'.isdecimal() is True
assert '¼'.isnumeric() is True        # Numeric_Type=Numeric
assert '¼'.isdigit() is False
assert 'Ⅻ'.isnumeric() is True        # Nl
assert '一'.isnumeric() is True       # CJK numeral, Numeric_Type=Numeric
assert '一'.isdigit() is False
assert 'a1²'.isalnum() is True

# isspace: CPython's explicit list includes U+001C-U+001F
assert '\x1c'.isspace() is True
assert '\x1d'.isspace() is True
assert '\x1e'.isspace() is True
assert '\x1f'.isspace() is True
assert '\x0b'.isspace() is True
assert '\x85'.isspace() is True
assert '\xa0'.isspace() is True
assert '\u200b'.isspace() is False
assert 'a\x1c'.isspace() is False

# isupper/islower: the derived Other_Uppercase / Other_Lowercase properties
assert 'Ⅻ'.isupper() is True          # Nl + Other_Uppercase
assert 'Ⅻ'.islower() is False
assert 'ª'.islower() is True          # Lo + Other_Lowercase
assert 'ª'.isupper() is False
assert 'ʰ'.islower() is True          # Lm + Other_Lowercase
assert 'AⅫ'.isupper() is True
assert 'Aª'.islower() is False

# istitle treats titlecase as upper in the state machine
assert 'ǅ'.istitle() is True          # Lt
assert 'Aǅ'.istitle() is False
assert 'ǅ'.isupper() is False
assert 'ǅ'.islower() is False

# repr/ascii escape private-use code points instead of emitting them
assert repr('\ue000') == "'\\ue000'"
assert repr('\uf8ff') == "'\\uf8ff'"
assert repr('\U000f0000') == "'\\U000f0000'"
assert repr('\u0378') == "'\\u0378'"  # unassigned
assert '\ue000'.isprintable() is False
assert ' '.isprintable() is True
assert ''.isprintable() is True

print("test_str_unicode_predicates passed")
