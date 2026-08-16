"""
Regression: oct() / format(v, 'b'|'o') must match CPython for all sizes,
including multi-byte boundary values (bit-extraction rewrite of
ToOctString / ToDigitsInBase; previously repeated BigInteger division made
large values O(n^2) slow, now O(n)).

CPython 3.14 reference values are hardcoded below.
"""

# oct(): single-byte and octal-digit boundaries
assert oct(0) == '0o0'
assert oct(1) == '0o1'
assert oct(7) == '0o7'
assert oct(8) == '0o10'
assert oct(63) == '0o77'
assert oct(64) == '0o100'
assert oct(0x7F) == '0o177'
assert oct(0x80) == '0o200'
assert oct(0xFF) == '0o377'
assert oct(0x100) == '0o400'
assert oct(0x1FF) == '0o777'
assert oct(0x200) == '0o1000'
assert oct(0x7FF) == '0o3777'
assert oct(0x800) == '0o4000'
assert oct(0xFFF) == '0o7777'
assert oct(0x1000) == '0o10000'
assert oct(0x7FFF) == '0o77777'
assert oct(0x8000) == '0o100000'
assert oct(0xFFFF) == '0o177777'
assert oct(0x10000) == '0o200000'
assert oct(0xFFFFFF) == '0o77777777'
assert oct(0x1000000) == '0o100000000'
assert oct(0x7FFFFFFF) == '0o17777777777'
assert oct(0x80000000) == '0o20000000000'
assert oct(-1) == '-0o1'
assert oct(-7) == '-0o7'
assert oct(-8) == '-0o10'
assert oct(-0x80) == '-0o200'
assert oct(-0x100) == '-0o400'
assert oct(-0x8000) == '-0o100000'
assert oct(-0x10000) == '-0o200000'
assert oct(-0x80000000) == '-0o20000000000'

# oct(): large multi-byte / multi-group values
assert oct(1 << 100) == '0o2' + '0' * 33
assert oct((1 << 100) - 1) == '0o1' + '7' * 33
assert oct(1 << 101) == '0o4' + '0' * 33
assert oct(1 << 102) == '0o1' + '0' * 34
assert oct(-(1 << 100)) == '-0o2' + '0' * 33
assert oct(10 ** 40) == '0o165431237070327122277527347653020000000000000'

# format(v, 'o'): int.__format__ octal path
assert format(0, 'o') == '0'
assert format(7, 'o') == '7'
assert format(8, 'o') == '10'
assert format(0x80, 'o') == '200'
assert format(0x100, 'o') == '400'
assert format(0x8000, 'o') == '100000'
assert format(0x10000, 'o') == '200000'
assert format(-0x80, 'o') == '-200'
assert format(0x100, '#o') == '0o400'
assert format(-0x100, '#o') == '-0o400'
assert format(1 << 100, 'o') == '2' + '0' * 33
assert format((1 << 100) - 1, 'o') == '1' + '7' * 33
assert format(1 << 102, 'o') == '1' + '0' * 34

# format(v, 'b'): int.__format__ binary path (same rewrite)
assert format(0, 'b') == '0'
assert format(1, 'b') == '1'
assert format(0x7F, 'b') == '1111111'
assert format(0x80, 'b') == '10000000'
assert format(0xFF, 'b') == '11111111'
assert format(0x100, 'b') == '100000000'
assert format(0x10000, 'b') == '10000000000000000'
assert format(-0x80, 'b') == '-10000000'
assert format(1 << 100, 'b') == '1' + '0' * 100
assert format((1 << 100) - 1, 'b') == '1' * 100
assert format(0xABCDEF, 'b') == '101010111100110111101111'

print("test_oct_bitshift_regression passed")
