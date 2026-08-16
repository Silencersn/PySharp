"""
Regression: bin()/hex() on negative integers whose bit length is a multiple
of 8 must not crash (Debug.Assert / FailFast) and must produce CPython's
output.

CPython 3.14 reference:
    bin(-128)   == '-0b10000000'
    bin(-255)   == '-0b11111111'
    bin(-32768) == '-0b1000000000000000'
    hex(-255)   == '-0xff'
    hex(-128)   == '-0x80'
    hex(-32768) == '-0x8000'
"""

# Crash boundary: |value| bit length is a multiple of 8 and value is negative
assert bin(-128) == '-0b10000000'
assert bin(-255) == '-0b11111111'
assert bin(-32768) == '-0b1000000000000000'
assert bin(-0x8000) == '-0b1000000000000000'
assert hex(-128) == '-0x80'
assert hex(-255) == '-0xff'
assert hex(-32768) == '-0x8000'
assert hex(-0x8000) == '-0x8000'

# large crash-boundary values (64-bit)
assert bin(-(1 << 63)) == '-0b' + '1' + '0' * 63
assert hex(-(1 << 63)) == '-0x8000000000000000'

# bit lengths that are NOT multiples of 8 must be unaffected
assert bin(-1) == '-0b1'
assert bin(-2) == '-0b10'
assert bin(-256) == '-0b100000000'
assert bin(-257) == '-0b100000001'
assert bin(-65536) == '-0b10000000000000000'
assert hex(-16) == '-0x10'
assert hex(-256) == '-0x100'
assert hex(-65536) == '-0x10000'

# positive controls (same MSB-set boundary) must keep working
assert bin(128) == '0b10000000'
assert bin(255) == '0b11111111'
assert bin(32768) == '0b1000000000000000'
assert hex(128) == '0x80'
assert hex(255) == '0xff'
assert hex(32768) == '0x8000'

# oct uses a different code path and must stay correct
assert oct(-0x80) == '-0o200'
assert oct(255) == '0o377'

# large values well beyond the byte boundary
assert bin(-(1 << 64)) == '-0b' + '1' + '0' * 64
assert hex(-(1 << 64)) == '-0x1' + '0' * 16
assert bin(1 << 64) == '0b1' + '0' * 64

print("test_bin_hex_neg_regression passed")
