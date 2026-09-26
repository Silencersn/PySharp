"""
upper/lower/casefold/title/swapcase/capitalize use
CPython's full case mappings (multi-character results, the derived
properties, the titlecase mapping and the Final_Sigma rule).

CPython 3.14 reference values are inline in the assertions.

:kind: test
"""

# full mappings that expand to several characters
assert 'ß'.upper() == 'SS'
assert 'ß'.casefold() == 'ss'
assert 'ß'.title() == 'Ss'
assert 'ﬁ'.upper() == 'FI'
assert 'ﬁ'.casefold() == 'fi'
assert '\u1e9e'.lower() == 'ß'         # capital sharp s
assert '\u1e9e'.casefold() == 'ss'
assert 'İ'.lower() == 'i\u0307'        # U+0130
assert 'İ'.casefold() == 'i\u0307'
assert 'ǰ'.upper() == 'J\u030c'
assert 'ᾲ'.upper() == '\u1fba\u0399'
assert 'ﬅ'.upper() == 'ST'

# titlecase mapping (not uppercase) for the first character
assert 'ǆx'.capitalize() == 'ǅx'
assert 'ǆx'.title() == 'ǅx'
assert 'ǅǆ'.title() == 'ǅǆ'
assert 'ǳǳ'.title() == 'ǲǳ'
assert 'ABC def ǆ'.title() == 'Abc Def ǅ'
assert 'ABC def ǆ'.capitalize() == 'Abc def ǆ'

# Final_Sigma: a capital sigma after a cased letter and before a
# non-cased character lowercases to the final form
assert 'ΣΣ'.lower() == 'σς'
assert 'σς'.lower() == 'σς'
assert 'ΣΣ'.title() == 'Σς'
assert 'ΣΣ'.swapcase() == 'σς'
assert 'ΣΣ'.capitalize() == 'Σς'
assert 'Σ'.lower() == 'σ'
assert 'Σ '.lower() == 'σ '
assert 'AΣ'.lower() == 'aς'
assert 'AΣB'.lower() == 'aσb'
assert 'ΣΣ'.casefold() == 'σσ'         # casefold has no Final_Sigma rule

# swapcase maps upper down and lower up, leaving titlecase alone
assert 'ß'.swapcase() == 'SS'
assert 'ǅ'.swapcase() == 'ǅ'
assert 'aBc'.swapcase() == 'AbC'

print("test_str_case_mapping passed")
