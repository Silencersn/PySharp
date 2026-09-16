# Regression: bytes(str, encoding, errors) encodes like str.encode —
# the constructor accepts the encoding/errors pair positionally or by
# keyword, bytearray(str, encoding) shares the same branch, and the
# CPython clinic faces (arity, keyword names, str type checks, the
# without-a-string guards) report byte-exact messages.

faces = [
    # the issue matrix
    ("bytes('a', encoding='utf-8')", repr(b'a')),
    ("bytes(chr(233), 'latin-1')", repr(b'\xe9')),
    ("bytes('a' + chr(233), 'utf-8')", repr(b'a\xc3\xa9')),
    ("'a'.encode('utf-8')", repr(b'a')),
    ("bytes('a', 'utf-8', 'strict')", repr(b'a')),
    ("bytes('a' + chr(233), 'ascii', 'replace')", repr(b'a?')),
    ("bytes('a' + chr(233), 'ascii', 'ignore')", repr(b'a')),
    ("bytes('a' + chr(233), 'ascii', 'xmlcharrefreplace')", repr(b'a&#233;')),
    ("bytes('a' + chr(233), 'ascii', 'backslashreplace')", repr(b'a\\xe9')),
    ("bytes('a', 'utf-8', errors='bogus')", repr(b'a')),
    ("bytearray('ab', 'utf-8')", repr(bytearray(b'ab'))),
    ("bytearray(chr(233), 'latin-1')", repr(bytearray(b'\xe9'))),
    ("bytearray('ab', encoding='utf-8')", repr(bytearray(b'ab'))),
    ("bytes('a', 'utf-16-le')", repr(b'a\x00')),
    ("bytes('a', 'utf-16')", repr(b'\xff\xfea\x00')),
    ("bytes('a', 'mbcs')", repr(b'a')),
    ("bytes('a', 'cp1251')", repr(b'a')),
    # untouched conversions
    ("bytes()", repr(b'')),
    ("bytes([65, 66])", repr(b'AB')),
    ("bytes(b'ab')", repr(b'ab')),
    ("bytes(3)", repr(b'\x00\x00\x00')),
    ("bytes('a', 'utf-8', 'strict', 'x')", "TypeError: bytes() takes at most 3 arguments (4 given)"),
    ("bytes('a', 'utf-8', errors='strict', encoding=None)", "TypeError: bytes() takes at most 3 arguments (4 given)"),
    ("bytes('a', encoding='utf-8', bogus=1)", "TypeError: bytes() got an unexpected keyword argument 'bogus'"),
    ("bytes('a', 'utf-8', encoding='x')", "TypeError: argument for bytes() given by name ('encoding') and position (2)"),
    ("bytes('a', encoding=None)", "TypeError: bytes() argument 'encoding' must be str, not None"),
    ("bytes('a', 1)", "TypeError: bytes() argument 'encoding' must be str, not int"),
    ("bytes('a', 'utf-8', 1)", "TypeError: bytes() argument 'errors' must be str, not int"),
    ("bytes('a')", "TypeError: string argument without an encoding"),
    ("bytes('a', errors='strict')", "TypeError: string argument without an encoding"),
    ("bytes(encoding='utf-8')", "TypeError: encoding without a string argument"),
    ("bytes(5, 'utf-8')", "TypeError: encoding without a string argument"),
    ("bytes(b'ab', 'utf-8')", "TypeError: encoding without a string argument"),
    ("bytes(errors='strict')", "TypeError: errors without a string argument"),
    ("bytes(None)", "TypeError: cannot convert 'NoneType' object to bytes"),
    ("bytes('a', 'bogus-codec')", "LookupError: unknown encoding: bogus-codec"),
    ("bytes(chr(233) + chr(0x4e2d), 'ascii', 'bogus-handler')", "LookupError: unknown error handler name 'bogus-handler'"),
    ("bytearray('ab')", "TypeError: string argument without an encoding"),
    ("bytearray('a', 1)", "TypeError: bytearray() argument 'encoding' must be str, not int"),
    ("bytearray('a', 'utf-8', 'strict', 'x')", "TypeError: bytearray() takes at most 3 arguments (4 given)"),
    ("bytearray(5, 'utf-8')", "TypeError: encoding without a string argument"),
    ("bytearray('ab', bogus=1)", "TypeError: bytearray() got an unexpected keyword argument 'bogus'"),
    # str.encode shares the core: strict 3.14 converters and codec lookup
    ("'a'.encode(encoding=None)", "TypeError: encode() argument 'encoding' must be str, not None"),
    ("'a'.encode(errors=None)", "TypeError: encode() argument 'errors' must be str, not None"),
    ("'a'.encode(encoding=1)", "TypeError: encode() argument 'encoding' must be str, not int"),
    ("'a'.encode('ascii', 1)", "TypeError: encode() argument 'errors' must be str, not int"),
    ("'a'.encode('bogus')", "LookupError: unknown encoding: bogus"),
    ("'a'.encode('utf-8')", repr(b'a')),
    ("''.encode('utf-16')", repr(b'\xff\xfe')),
    ("bytes(source='a', encoding='utf-8')", repr(b'a')),
]

for expr, expected in faces:
    try:
        got = repr(eval(expr))
    except Exception as e:
        got = type(e).__name__ + ": " + str(e)
    assert got == expected, (expr, got, expected)

print("codec constructor faces ok,", len(faces), "cases")
print("test_bytes_str_encoding_regression passed")
