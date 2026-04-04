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
