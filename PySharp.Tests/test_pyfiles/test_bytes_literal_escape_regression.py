r"""
Regression: bytes literal escapes must follow CPython's _PyBytes_DecodeEscape2
semantics, NOT the str escape decoder.

CPython 3.14 reference:
    b'\777'        -> b'\xff'   (0o777 = 511 truncated to low 8 bits) + SyntaxWarning
    b'\400'        -> b'\x00'   (0o400 = 256 truncated) + SyntaxWarning
    b'\377'        -> b'\xff'   (in range, no warning)
    b'\101'        -> b'A'      (0o101 = 65)
    b'\000'        -> b'\x00'
    b'\ud800'      -> b'\\ud800'   (\u is NOT a bytes escape; kept literally) + SyntaxWarning
    b'\U0001F600'  -> b'\\U0001F600' + SyntaxWarning
    b'\z'          -> b'\\z'    (unknown escape kept literally) + SyntaxWarning
    b'\x41'        -> b'A'
    b'\x'          -> SyntaxError (truncated \x escape)
    br'\777'       -> b'\\777'  (raw bytes keep the backslash)

Previously PySharp decoded bytes literals with the str escape decoder and
rejected any decoded character above 0xFF with SyntaxError, so b'\777',
b'\400', b'\ud800' and b'\U0001F600' failed to compile instead of following
CPython's truncate / keep-literal rules.
"""

# --- octal escapes (greedy 3 digits) ---
assert b'\000' == b'\x00'
assert b'\101' == b'A'
assert b'\141' == b'a'
assert b'\377' == b'\xff'

# --- octal > 0o377: truncated to the low 8 bits (+ SyntaxWarning in CPython) ---
assert b'\777' == b'\xff'
assert b'\400' == b'\x00'

# --- \u / \U are NOT bytes escapes: kept literally (+ SyntaxWarning) ---
assert b'\ud800' == b'\\ud800'
assert b'\U0001F600' == b'\\U0001F600'

# --- unknown escape kept literally (+ SyntaxWarning) ---
assert b'\z' == b'\\z'

# --- \x hex escapes (2 digits) ---
assert b'\x41' == b'A'
assert b'\x00' == b'\x00'

# --- common escapes ---
assert b'\n' == b'\x0a'
assert b'\t' == b'\x09'
assert b'\\' == b'\x5c'

# --- raw bytes keep the backslash ---
assert br'\777' == b'\\777'
assert br'\ud800' == b'\\ud800'

# --- truncated \x must still raise SyntaxError (matches CPython) ---
try:
    eval("b'\\x'")
except SyntaxError:
    pass
else:
    raise AssertionError("b'\\x' should raise SyntaxError")

print("test_bytes_literal_escape_regression passed")
