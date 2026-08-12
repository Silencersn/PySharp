"""
Regression: incompatible string-prefix combinations (fB, tb, fu, ...) must
not be silently accepted as f-string/t-string prefixes.

CPython 3.14 reference:
- Valid f/t-string prefixes are only the single letters f/F/t/T, plus the
  combinations with r/R (raw), in either order and any casing:
      fr fR Fr FR  rf rF Rf RF
      tr tR Tr TR  rt rT Rt RT
- Any other 2-letter combination raises SyntaxError at compile time:
      fB'abc' -> SyntaxError: 'b' and 'f' prefixes are incompatible
      tb'abc' -> SyntaxError: 'b' and 't' prefixes are incompatible
      fu'abc' -> SyntaxError: 'u' and 'f' prefixes are incompatible
- Bytes prefixes (b, br, rb, rB, ...) are unrelated to f/t-strings and must
  keep lexing as bytes literals.

Previously the lexer accepted any 2-character "prefix" whose second character
was a prefix letter (b/B/f/F/t/T/r/R/u/U), so fB'...' / tb'...' / fu'...'
etc. were silently compiled as f/t-strings instead of being rejected.
"""

# --- single-letter f/t-string prefixes keep working ---
assert f'{1}' == '1'
assert F'{1}' == '1'
assert t'x{1}y' is not None
assert T'x{1}y' is not None

# --- valid f/t + r/R combinations (all 16 case variants) keep working ---
assert fr'x{1}y' == 'x1y'
assert fR'x{1}y' == 'x1y'
assert Fr'x{1}y' == 'x1y'
assert FR'x{1}y' == 'x1y'
assert rf'x{2}y' == 'x2y'
assert rF'x{2}y' == 'x2y'
assert Rf'x{2}y' == 'x2y'
assert RF'x{2}y' == 'x2y'
assert tr'x{3}y' is not None
assert tR'x{3}y' is not None
assert Tr'x{3}y' is not None
assert TR'x{3}y' is not None
assert rt'x{4}y' is not None
assert rT'x{4}y' is not None
assert Rt'x{4}y' is not None
assert RT'x{4}y' is not None

# --- unrelated string/bytes prefixes keep working ---
assert u'abc' == 'abc'
assert r'abc' == 'abc'
assert b'abc' == b'abc'
assert br'abc' == b'abc'
assert rb'abc' == b'abc'
assert rB'abc' == b'abc'
assert RB'abc' == b'abc'

# --- incompatible prefix combinations must be rejected (SyntaxError) ---
# These must NOT be silently accepted as f/t-strings (the pre-fix behavior).
invalid = [
    "fB'abc'", "fb'abc'", "tb'abc'", "bt'abc'",
    "fu'abc'", "uf'abc'", "tu'abc'", "ut'abc'",
    "bf'abc'", "ft'abc'", "tf'abc'", "ub'abc'",
    "ur'abc'", "ru'abc'",
]

for src in invalid:
    try:
        eval(src)
    except SyntaxError:
        pass
    else:
        raise AssertionError(f"expected SyntaxError for {src!r}, but it was accepted")

print("test_fstring_prefix_combo_regression passed")
