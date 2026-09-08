# __contains__ on bytes/bytearray: bytes-like operands (bytes,
# bytearray, memoryview) are searched as subsequences in both cross
# directions, int operands are byte membership with the legacy
# 0..255 validation, and everything else gets the buffer TypeError.
assert (b'ab' in bytearray(b'xaby')) is True
assert (b'z' in bytearray(b'xaby')) is False
assert (bytearray(b'ab') in b'xaby') is True
assert (bytearray(b'z') in b'xaby') is False
assert (bytearray(b'ab') in bytearray(b'xaby')) is True
assert (memoryview(b'ab') in b'xaby') is True
assert (memoryview(b'ab') in bytearray(b'xaby')) is True
assert (b'' in b'abc') is True
assert (b'' in bytearray(b'abc')) is True

assert (97 in b'abc') is True
assert (97 in bytearray(b'abc')) is True
assert (True in b'abc') is False   # bool is int 1, not present
assert (0 in b'abc') is False

class Idx:
    def __index__(self): return 98

assert (Idx() in b'abc') is True
assert (Idx() in bytearray(b'abc')) is True

def expect(exc_type, msg, fn):
    try:
        fn()
    except exc_type as e:
        assert str(e) == msg, str(e)
        return
    raise AssertionError("expected " + exc_type.__name__)

for operand in [300, -1]:
    expect(ValueError, "byte must be in range(0, 256)", lambda o=operand: o in b'abc')
    expect(ValueError, "byte must be in range(0, 256)", lambda o=operand: o in bytearray(b'abc'))

expect(TypeError, "a bytes-like object is required, not 'str'", lambda: 'a' in b'abc')
expect(TypeError, "a bytes-like object is required, not 'str'", lambda: 'a' in bytearray(b'abc'))
expect(TypeError, "a bytes-like object is required, not 'list'", lambda: [97] in b'abc')
expect(TypeError, "a bytes-like object is required, not 'float'", lambda: 97.0 in b'abc')

# __add__: bytes accepts any bytes-like operand and stays bytes
r = b'a' + bytearray(b'b')
assert r == b'ab' and type(r) is bytes
r = b'a' + memoryview(b'b')
assert r == b'ab' and type(r) is bytes
r = bytearray(b'a') + memoryview(b'b')
assert r == bytearray(b'ab') and type(r) is bytearray
r = bytearray(b'a') + b'b'
assert r == bytearray(b'ab') and type(r) is bytearray

expect(TypeError, "can't concat str to bytes", lambda: b'a' + 'b')
expect(TypeError, "can't concat int to bytes", lambda: b'a' + 1)
expect(TypeError, "can't concat str to bytearray", lambda: bytearray(b'a') + 'b')

# bytearray byte-value paths use the singular message
expect(ValueError, "byte must be in range(0, 256)", lambda: bytearray([300]))
ba = bytearray()
expect(ValueError, "byte must be in range(0, 256)", lambda: ba.append(300))
expect(ValueError, "bytes must be in range(0, 256)", lambda: bytes([300]))
print("ok")
