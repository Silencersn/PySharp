"""
Regression: bytes.decode error handling.

The default errors='strict' raises UnicodeDecodeError on invalid input
(no more silent replacement), and the errors= parameter is accepted in
both keyword and positional form with working strict/ignore/replace/
backslashreplace/surrogateescape/surrogatepass handlers. The exception
carries encoding/object/start/end/reason and str() matches CPython's
single-byte and position-range forms.

CPython 3.14 reference (Objects/unicodeobject.c PyUnicode_DecodeUTF8
and friends, Objects/exceptions.c UnicodeDecodeError_str).
"""

# strict faces: exact messages
for expr, expected in [
    ("b'\\xff\\xfe'.decode('utf-8')",
     "'utf-8' codec can't decode byte 0xff in position 0: invalid start byte"),
    ("b'abc\\xff'.decode('ascii')",
     "'ascii' codec can't decode byte 0xff in position 3: ordinal not in range(128)"),
    ("b'\\xc3'.decode('utf-8')",
     "'utf-8' codec can't decode byte 0xc3 in position 0: unexpected end of data"),
    ("b'\\xe0\\x80'.decode('utf-8')",
     "'utf-8' codec can't decode byte 0xe0 in position 0: invalid continuation byte"),
    ("b'\\xf0\\x90'.decode('utf-8')",
     "'utf-8' codec can't decode bytes in position 0-1: unexpected end of data"),
    ("b'\\xc0\\xaf'.decode('utf-8')",
     "'utf-8' codec can't decode byte 0xc0 in position 0: invalid start byte"),
    ("b'\\xed\\xa0\\x80'.decode('utf-8')",
     "'utf-8' codec can't decode byte 0xed in position 0: invalid continuation byte"),
    ("b'\\xf5\\x80'.decode('utf-8')",
     "'utf-8' codec can't decode byte 0xf5 in position 0: invalid start byte"),
]:
    try:
        eval(expr)
        assert False, f"UnicodeDecodeError expected for {expr}"
    except UnicodeDecodeError as e:
        assert str(e) == expected, f"{expr}: {str(e)!r}"

# valid input keeps decoding
assert b'abc'.decode('utf-8') == 'abc'
assert b''.decode('utf-8') == ''
assert b'hello'.decode('ascii') == 'hello'
assert b'\xff'.decode('latin-1') == 'ÿ'

# errors= is accepted by keyword and position; handlers work
assert b'\xff'.decode('utf-8', errors='replace') == '\ufffd'
assert b'\xff'.decode('utf-8', 'replace') == '\ufffd'
assert b'a\xffb'.decode('utf-8', errors='ignore') == 'ab'
assert b'a\xffb'.decode('utf-8', 'ignore') == 'ab'
assert b'\xff\xfe'.decode('utf-8', 'replace') == '\ufffd\ufffd'
assert b'\xc0\xaf'.decode('utf-8', 'replace') == '\ufffd\ufffd'
assert b'\xc3'.decode('utf-8', 'replace') == '\ufffd'
assert b'abc\xff'.decode('ascii', 'replace') == 'abc\ufffd'
assert b'abc\xff'.decode('ascii', 'ignore') == 'abc'
assert b'\xff'.decode('utf-8', 'backslashreplace') == '\\xff'

# unknown handler: valid input is fine, an error surfaces the LookupError
assert b'abc'.decode('utf-8', 'bogus') == 'abc'
try:
    b'\xff'.decode('utf-8', 'bogus')
    assert False, "LookupError expected"
except LookupError as e:
    assert str(e) == "unknown error handler name 'bogus'"

# aliases report the canonical codec name
for alias in ('UTF-8', 'utf_8'):
    try:
        b'\xff'.decode(alias)
        assert False, "UnicodeDecodeError expected"
    except UnicodeDecodeError as e:
        assert str(e) == "'utf-8' codec can't decode byte 0xff in position 0: invalid start byte"
try:
    b'\xff'.decode('us-ascii')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert str(e) == "'ascii' codec can't decode byte 0xff in position 0: ordinal not in range(128)"

# exception attributes and args
try:
    b'abc\xff'.decode('ascii')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert e.encoding == 'ascii'
    assert e.object == b'abc\xff'
    assert e.start == 3
    assert e.end == 4
    assert e.reason == 'ordinal not in range(128)'
    assert e.args == ('ascii', b'abc\xff', 3, 4, 'ordinal not in range(128)')

# catchability through the UnicodeError/ValueError hierarchy
try:
    b'\xff'.decode('utf-8')
    assert False
except UnicodeError:
    pass
try:
    b'\xff'.decode('utf-8')
    assert False
except ValueError:
    pass

# str(bytes, encoding, errors) constructor form
assert str(b'\xff', 'utf-8', 'replace') == '\ufffd'
assert str(b'\xff', 'utf-8', errors='ignore') == ''
try:
    str(b'\xff', 'utf-8')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError:
    pass

# utf-16/32 strict and BOM faces
try:
    b'\x00\xd8'.decode('utf-16-le')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert str(e) == "'utf-16-le' codec can't decode bytes in position 0-1: unexpected end of data"
try:
    b'\x00\xdcA\x00'.decode('utf-16-le')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert str(e) == "'utf-16-le' codec can't decode bytes in position 0-1: illegal encoding"
try:
    b'\x00\xd8A\x00'.decode('utf-16-le')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert str(e) == "'utf-16-le' codec can't decode bytes in position 0-1: illegal UTF-16 surrogate"
try:
    b'\x00'.decode('utf-16-le')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert str(e) == "'utf-16-le' codec can't decode byte 0x00 in position 0: truncated data"
assert b'A\x00B\x00'.decode('utf-16-le') == 'AB'
assert b'\xff\xfeA\x00B\x00'.decode('utf-16') == 'AB'
assert b'A\x00\x00\x00B\x00\x00\x00'.decode('utf-32-le') == 'AB'
assert b'A\x00\x00\x00\xff\x00\x00\x00'.decode('utf-32-le') == 'A\xff'

# surrogateescape: values verified against %c-built strings (the repr/ord
# pipeline cannot display lone surrogates)
assert b'\xff'.decode('utf-8', 'surrogateescape') == '%c' % 0xDCFF
assert b'\xff\xfe'.decode('utf-8', 'surrogateescape') == '%c%c' % (0xDCFF, 0xDCFE)

# surrogatepass: encoded surrogate sequences pass, other errors stay strict
assert b'\xed\xa0\x80'.decode('utf-8', 'surrogatepass') == '%c' % 0xD800
try:
    b'\xff'.decode('utf-8', 'surrogatepass')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError as e:
    assert str(e) == "'utf-8' codec can't decode byte 0xff in position 0: invalid start byte"

# keyword combinations
assert b'\xff'.decode(encoding='utf-8', errors='replace') == '\ufffd'
try:
    b'\xff'.decode(encoding='utf-8')
    assert False, "UnicodeDecodeError expected"
except UnicodeDecodeError:
    pass
assert b'\xff'.decode(errors='replace') == '\ufffd'

print("test_bytes_decode_errors_regression passed")
