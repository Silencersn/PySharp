"""
Regression: format()/f-string int 'x'/'X' must not retain .NET's sign-bit
leading '0' for values whose MSB is set.

CPython 3.14 reference:
    format(255, 'x')     == 'ff'
    format(128, 'x')     == '80'
    format(255, 'X')     == 'FF'
    format(255, '#x')    == '0xff'
    format(-255, 'x')    == '-ff'
    format(0xABCDEF, 'x') == 'abcdef'
"""

# MSB-set values (bit length % 8 == 0) must have no leading zero
assert format(255, 'x') == 'ff'
assert format(128, 'x') == '80'
assert format(255, 'X') == 'FF'
assert format(255, '#x') == '0xff'
assert format(255, '#X') == '0XFF'
assert format(-255, 'x') == '-ff'
assert format(-128, '#x') == '-0x80'
assert format(0x8000, 'x') == '8000'
assert format(-0x8000, 'x') == '-8000'
assert format(0xFFFF, 'x') == 'ffff'
assert format(0xABCDEF, 'x') == 'abcdef'
assert format(0x10000, 'x') == '10000'

# controls: values without MSB set / zero / other bases
assert format(127, 'x') == '7f'
assert format(256, 'x') == '100'
assert format(0, 'x') == '0'
assert format(0, '#x') == '0x0'
assert format(255, 'b') == '11111111'
assert format(255, 'o') == '377'

# zero-padding (masks the bug) must stay correct
assert format(255, '08x') == '000000ff'

# space-padding (default right-align) must align correctly
assert format(255, '3x') == ' ff'
assert format(255, '4x') == '  ff'
assert format(255, '6x') == '    ff'
assert format(255, '#7x') == '   0xff'
assert format(0xABCDEF, '7x') == ' abcdef'

# f-string equivalents
assert f'{255:x}' == 'ff'
assert f'{255:#x}' == '0xff'
assert f'{-255:x}' == '-ff'
assert f'{0x8000:x}' == '8000'

print("test_int_hex_leading_zero_regression passed")
