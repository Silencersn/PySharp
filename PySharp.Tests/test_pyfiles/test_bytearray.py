# Basic construction
b = bytearray()
assert repr(b) == "bytearray(b'')"
assert len(b) == 0

b = bytearray([97, 98, 99])
assert repr(b) == "bytearray(b'abc')"
assert len(b) == 3

b2 = bytearray(b)
b[0] = 120
assert b2 == bytearray(b'abc')
assert b == bytearray(b'xbc')

# Indexing and slicing
assert b[1] == 98
assert b[-1] == 99
assert b[1:] == bytearray(b'bc')
assert b[::-1] == bytearray(b'cbx')

# Item and slice assignment
b[1] = 121
assert b == bytearray(b'xyc')

b[1:3] = b'12'
assert b == bytearray(b'x12')

b = bytearray(b'abcdef')
b[::2] = b'XYZ'
assert b == bytearray(b'XbYdZf')

# Operators
assert bytearray(b'ab') + bytearray(b'cd') == bytearray(b'abcd')
assert bytearray(b'ab') + b'cd' == bytearray(b'abcd')

c = bytearray(b'a')
c += b'bc'
assert c == bytearray(b'abc')

assert bytearray(b'ab') * 3 == bytearray(b'ababab')

d = bytearray(b'ab')
d *= 2
assert d == bytearray(b'abab')

# Methods
m = bytearray(b'a')
m.append(98)
m.extend([99, 100])
assert m == bytearray(b'abcd')

# Errors
try:
    hash(bytearray(b'a'))
    assert False
except TypeError:
    pass

try:
    bytearray([256])
    assert False
except ValueError:
    pass

print("test_bytearray passed")
