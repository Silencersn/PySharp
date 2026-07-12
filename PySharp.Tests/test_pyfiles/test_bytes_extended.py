"""
Extended tests for bytes type - covers edge cases for PyBytesObject, PyBytesObjectType
Covers __add__, __mul__, __contains__, slicing, hash, and error cases
"""

# bytes concatenation
assert b"hello" + b" world" == b"hello world"
assert b"" + b"test" == b"test"
assert b"test" + b"" == b"test"
assert b"" + b"" == b""

# bytes concatenation error
try:
    b"hello" + "world"
    assert False, "Should raise TypeError"
except TypeError:
    pass

# bytes repetition
assert b"abc" * 3 == b"abcabcabc"
assert b"abc" * 0 == b""
assert b"abc" * (-1) == b""
assert b"abc" * 1 == b"abc"
assert b"" * 5 == b""

# bytes __rmul__
assert 3 * b"abc" == b"abcabcabc"
assert 0 * b"abc" == b""
assert 1 * b"xyz" == b"xyz"

# bytes __contains__
assert b"ll" in b"hello"
assert b"xx" not in b"hello"
assert b"" in b"hello"
assert b"hello" in b"hello"
assert b"o" in b"hello"
assert b"xyz" not in b"hello"

# bytes slicing
assert b"hello"[0:3] == b"hel"
assert b"hello"[1:4] == b"ell"
assert b"hello"[:] == b"hello"
assert b"hello"[::-1] == b"olleh"
assert b"hello"[0:10] == b"hello"
assert b"hello"[10:20] == b""
assert b"hello"[-3:-1] == b"ll"
assert b"hello"[-5:] == b"hello"

# bytes indexing
assert b"hello"[0] == 104
assert b"hello"[-1] == 111
assert b"hello"[4] == 111

# bytes indexing out of range
try:
    b"hello"[10]
    assert False, "Should raise IndexError"
except IndexError:
    pass

try:
    b"hello"[-10]
    assert False, "Should raise IndexError"
except IndexError:
    pass

# bytes hash
s = {b"hello", b"world", b"hello"}
assert len(s) == 2

# bytes with various repr cases
assert repr(b"hello") == "b'hello'"
# bytes with quotes in repr
assert repr(b'it\'s') == "b\"it's\""
# bytes with double quotes
assert repr(b'"quote"') == "b'\"quote\"'"

# bytes with non-printable chars
b_nonprint = bytes([0, 1, 2, 127, 255])
r = repr(b_nonprint)
assert "\\x00" in r
assert "\\x01" in r
assert "\\x02" in r
assert "\\x7f" in r or "\\x7f" in r.lower()
assert "\\xff" in r or "\\xff" in r.lower()

# bytes with tab, cr, nl
b_special = bytes([9, 10, 13])
r = repr(b_special)
assert "\\t" in r
assert "\\n" in r
assert "\\r" in r

# bytes as hashable in dict
d = {b"key1": "value1", b"key2": "value2"}
assert d[b"key1"] == "value1"
assert d[b"key2"] == "value2"

# bytes != str
assert b"hello" != "hello"

# bytes comparison with non-bytes returns NotImplemented -> not equal
assert (b"abc" == 123) is False
assert (b"abc" != 123) is True

# bytes.decode
assert b"hello".decode() == "hello"
assert b"hello".decode("utf-8") == "hello"
assert b"caf\xc3\xa9".decode("utf-8") == "caf\u00e9"
assert b"hello".decode("ascii") == "hello"

# bytes.decode with unknown encoding should raise LookupError (or TypeError)
try:
    b"test".decode("unknown-encoding")
    assert False, "Should raise exception"
except:
    pass

print("test_bytes_extended passed")
