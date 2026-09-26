"""
the bare 'utf-16' / 'utf-32' codecs must emit a native-order BOM
when encoding and treat a leading BOM as a byte order mark when decoding,
matching CPython. The explicit -le/-be variants stay BOM-free on encode and
leave a BOM in the decoded text.

CPython 3.14 reference:
    'a'.encode('utf-16')                  -> b'\\xff\\xfea\\x00'
    ''.encode('utf-16')                   -> b'\\xff\\xfe'
    'a'.encode('utf-32')                  -> b'\\xff\\xfe\\x00\\x00a\\x00\\x00\\x00'
    b'\\xfe\\xffa\\x00'.decode('utf-16')   -> '\\u6100' (BOM selects BE, dropped)
    b'\\xff\\xfe'.decode('utf-16')         -> ''      (bare BOM decodes empty)
    b'\\xff\\xfea\\x00'.decode('utf-16-be')-> '\\ufffe\\u6100' (BOM kept as data)

Previously the bare codecs emitted no BOM on either side, 'utf-32-le' /
'utf-32-be' were rejected as unknown encodings, and bytes.decode resolved
codec names without the dashed -le/-be normalization.

:kind: test
"""

# --- encode: bare codecs emit a BOM, even for the empty string ---
assert 'a'.encode('utf-16') == b'\xff\xfea\x00'
assert ''.encode('utf-16') == b'\xff\xfe'
assert '\U0001F600'.encode('utf-16') == b'\xff\xfe=\xd8\x00\xde'
assert 'a'.encode('utf-32') == b'\xff\xfe\x00\x00a\x00\x00\x00'
assert ''.encode('utf-32') == b'\xff\xfe\x00\x00'
assert '\U0001F600'.encode('utf-32') == b'\xff\xfe\x00\x00\x00\xf6\x01\x00'

# --- encode: explicit -le/-be variants stay BOM-free ---
assert 'abc'.encode('utf-16-le') == b'a\x00b\x00c\x00'
assert 'abc'.encode('utf-16-be') == b'\x00a\x00b\x00c'
assert 'abc'.encode('utf-32-le') == b'a\x00\x00\x00b\x00\x00\x00c\x00\x00\x00'
assert 'abc'.encode('utf-32-be') == b'\x00\x00\x00a\x00\x00\x00b\x00\x00\x00c'

# --- decode: a leading BOM selects the byte order and is dropped ---
assert b'\xff\xfea\x00'.decode('utf-16') == 'a'
assert b'\xfe\xffa\x00'.decode('utf-16') == '\u6100'
assert b'a\x00'.decode('utf-16') == 'a'
assert b'\xff\xfe'.decode('utf-16') == ''
assert b''.decode('utf-16') == ''
assert b'\xff\xfea\x00\xff\xfeb\x00'.decode('utf-16') == 'a\ufeffb'
assert b'\xff\xfe\x00\x00a\x00\x00\x00'.decode('utf-32') == 'a'
assert b'\xff\xfe\x00\x00'.decode('utf-32') == ''
assert b''.decode('utf-32') == ''

# --- decode: explicit -le/-be variants keep a BOM as ordinary data ---
assert b'a\x00'.decode('utf-16-le') == 'a'
assert b'\x00a'.decode('utf-16-be') == 'a'
assert b'\xff\xfea\x00'.decode('utf-16-be') == '\ufffe\u6100'
assert b'a\x00\x00\x00'.decode('utf-32-le') == 'a'
assert b'\x00\x00\x00a'.decode('utf-32-be') == 'a'

# --- round trips ---
s = 'a\u4e2d\U0001F600'
assert s.encode('utf-16').decode('utf-16') == s
assert s.encode('utf-32').decode('utf-32') == s
assert s.encode('utf-16-le').decode('utf-16-le') == s
assert s.encode('utf-32-be').decode('utf-32-be') == s

print("test_str_utf16_utf32_bom passed")
