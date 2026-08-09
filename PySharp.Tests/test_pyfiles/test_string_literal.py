assert '\\' == '\\'
assert '\'' == "'"
assert '\"' == '"'
assert '\a' == '\a'
assert '\b' == '\b'
assert '\f' == '\f'
assert '\n' == '\n'
assert '\r' == '\r'
assert '\t' == '\t'
assert '\v' == '\v'
assert '\x41' == 'A'
assert '\u0041' == 'A'
assert '\U00000041' == 'A'
assert '\101' == 'A'
assert '\0' == '\x00'
assert '\07' == '\x07'
assert '\x7a' == 'z'
assert '\u0061' == 'a'
assert '\U0000007a' == 'z'
assert '\n\t\r' == '\n\t\r'
assert '\\n' == r'\n'
assert '\\u0041' == r'\u0041'
assert '\\x41' == r'\x41'
assert '\\101' == r'\101'

assert b'' == b''
assert b'abc' == b'abc'
assert b'\x41' == b'A'
assert b'a' b'b' == b'ab'

try:
    eval("'\\x4'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\u041'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\U000041'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\xZZ'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\uZZZZ'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\UZZZZZZZZ'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\x'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\u'")
    assert False
except SyntaxError:
    pass

try:
    eval("'\\U'")
    assert False
except SyntaxError:
    pass

try:
    eval("b'a' 'b'")
    assert False
except SyntaxError:
    pass


# ===== Regression: octal escapes \ooo greedily read 3 digits (issue #21) =====
# CPython keeps values > 0o377 (e.g. '\777' -> U+01FF) and only emits a
# SyntaxWarning; PySharp used to read only 2 digits for a leading 4-7 digit.

assert '\777' == '\u01ff'       # 0o777 = U+01FF
assert '\400' == '\u0100'       # 0o400 = U+0100
assert '\377' == '\u00ff'       # 0o377 = U+00FF (no warning)
assert '\770' == '\u01f8'       # 0o770 = U+01F8
assert '\407' == '\u0107'       # 0o407 = U+0107
assert '\778' == '?8'           # reads 2 digits, then literal '8'
assert '\7779' == '\u01ff9'     # reads 3 digits, then literal '9'


# ===== Regression: \u / \U lone surrogates allowed (issue #22) =====
# CPython allows lone surrogates in string literals; only encode('utf-8')
# raises UnicodeEncodeError at runtime. PySharp used to reject them at
# compile time with UnicodeEncodeError / SyntaxError.

assert '\ud800' == '\ud800'
assert '\udfff' == '\udfff'
assert '\U0000d800' == '\ud800'
assert '\U0000dfff' == '\udfff'
assert '\U0001f600' == '\U0001f600'
assert len('\ud800') == 1
