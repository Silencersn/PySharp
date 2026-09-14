"""
Regression: int() accepts bytes-like arguments (bytes, bytearray,
memoryview), parsing their ASCII characters with the given base like
CPython PyNumber_Long. The old implementation rejected bytes with the
self-contradictory TypeError whose message claims a bytes-like object
is accepted; int(b'ff', 16) also failed.

CPython 3.14 reference: invalid literals raise ValueError quoting the
BYTES repr (b'...'), even for bytearray/memoryview arguments.
"""

def err(fn):
    try:
        return ('ok', fn())
    except Exception as e:
        return (type(e).__name__, str(e))

# basic parsing
assert int(b'10') == 10
assert int(b'ff', 16) == 255
assert int(b'-5') == -5
assert int(b'+3') == 3
assert int(b'  12  ') == 12
assert int(b'1_0') == 10
assert int(b'0x10', 0) == 16 and int(b'0b101', 0) == 5

# bytearray and memoryview parse the same way
assert int(bytearray(b'42')) == 42
assert int(bytearray(b'ff'), 16) == 255
assert int(memoryview(b'10')) == 10

# errors quote the bytes repr
assert err(lambda: int(b'1x')) == ('ValueError', "invalid literal for int() with base 10: b'1x'")
assert err(lambda: int(b'1x', 16)) == ('ValueError', "invalid literal for int() with base 16: b'1x'")
assert err(lambda: int(bytearray(b'1x'))) == ('ValueError', "invalid literal for int() with base 10: b'1x'")
assert err(lambda: int(b'\xff')) == ('ValueError', "invalid literal for int() with base 10: b'\\xff'")
assert err(lambda: int(b'')) == ('ValueError', "invalid literal for int() with base 10: b''")

# the digit limit applies to bytes too
message = err(lambda: int(b'1' * 5000))
assert message[0] == 'ValueError' and 'Exceeds the limit' in message[1]

# non-bytes faces unchanged
assert err(lambda: int(b'10', 'x')) == ('TypeError', "'str' object cannot be interpreted as an integer")
assert err(lambda: int('zz')) == ('ValueError', "invalid literal for int() with base 10: 'zz'")
assert err(lambda: int(1.5)) == ('ok', 1)
assert int(True) == 1

print("test_int_bytes_regression passed")
