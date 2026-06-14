"""
Tests for bytes and bytearray types - covers PyBytesObject, PyBytesIterator, PyByteArrayObject, PyByteArrayIterator
"""

# bytes literal
b = b"hello"
assert isinstance(b, bytes)
assert len(b) == 5
assert b[0] == 104  # 'h'
assert b[4] == 111  # 'o'

# bytes constructor
assert bytes() == b''
assert bytes(b"abc") == b'abc'
assert bytes([104, 101, 108, 108, 111]) == b'hello'

# bytes iteration
result = []
for byte_val in b"ABC":
    result.append(byte_val)
assert result == [65, 66, 67]

# bytes repr
assert repr(b"hello") == "b'hello'"
assert repr(b'he"llo') == "b'he\"llo'"

# bytes comparison
assert b"abc" == b"abc"
assert b"abc" != b"xyz"
assert b"abc" < b"abd"

# bytes contains
assert b"ll" in b"hello"
assert b"xx" not in b"hello"

# bytearray
ba = bytearray(b"world")
assert isinstance(ba, bytearray)
assert len(ba) == 5
assert ba[0] == 119  # 'w'

# bytearray constructor
assert bytearray() == bytearray(b'')
assert bytearray(b"test") == bytearray(b"test")
assert bytearray([116, 101, 115, 116]) == bytearray(b"test")

# bytearray iteration
result = []
for byte_val in bytearray(b"XYZ"):
    result.append(byte_val)
assert result == [88, 89, 90]

# bytearray mutable
ba = bytearray(b"abc")
ba[0] = 122  # 'z'
assert ba == bytearray(b"zbc")

# bytes/bytearray error cases
try:
    bytes("hello")  # str without encoding
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    bytes([256])  # value out of range
    assert False, "Should raise ValueError"
except ValueError:
    pass

try:
    bytes([-1])  # negative value
    assert False, "Should raise ValueError"
except ValueError:
    pass

print("test_bytes passed")
