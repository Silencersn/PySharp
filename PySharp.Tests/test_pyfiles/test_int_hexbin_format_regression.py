"""
Regression: hex(0)/bin(0) must keep the digit, and int format()/f-string
with b/o/x must not double the prefix (issue #12).

CPython 3.14 reference:
    hex(0) == '0x0', bin(0) == '0b0', oct(0) == '0o0'
    format(42, 'x') == '2a', format(42, '#x') == '0x2a'
    format(0, 'x') == '0', format(0, '#x') == '0x0'
    format(-42, 'x') == '-2a', format(-42, '#x') == '-0x2a'
"""

# hex/bin/oct builtins on zero must keep the digit
assert hex(0) == '0x0'
assert bin(0) == '0b0'
assert oct(0) == '0o0'

# hex/bin/oct builtins on non-zero keep working
assert hex(42) == '0x2a'
assert bin(42) == '0b101010'
assert oct(42) == '0o52'
assert hex(-42) == '-0x2a'

# format: no prefix without '#'
assert format(42, 'x') == '2a'
assert format(42, 'X') == '2A'
assert format(42, 'b') == '101010'
assert format(42, 'o') == '52'

# format: single prefix with '#'
assert format(42, '#x') == '0x2a'
assert format(42, '#X') == '0X2A'
assert format(42, '#b') == '0b101010'
assert format(42, '#o') == '0o52'

# format: zero
assert format(0, 'x') == '0'
assert format(0, 'X') == '0'
assert format(0, 'b') == '0'
assert format(0, 'o') == '0'
assert format(0, '#x') == '0x0'
assert format(0, '#X') == '0X0'
assert format(0, '#b') == '0b0'
assert format(0, '#o') == '0o0'

# format: negative sign placement
assert format(-42, 'x') == '-2a'
assert format(-42, '#x') == '-0x2a'
assert format(-0, 'x') == '0'

# f-string equivalent
assert f'{0:x}' == '0'
assert f'{0:#x}' == '0x0'
assert f'{42:x}' == '2a'
assert f'{42:#x}' == '0x2a'
assert f'{-42:x}' == '-2a'
assert f'{-42:#x}' == '-0x2a'

print("test_int_hexbin_format_regression passed")
