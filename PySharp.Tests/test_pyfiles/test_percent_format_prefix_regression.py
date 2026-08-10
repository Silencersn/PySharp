"""
Regression: old-style %#o/%#x/%#X prefix handling must match CPython
(issue #20).

CPython 3.14 reference:
    '%#o' % -8      == '-0o10'   (sign then prefix)
    '%#x' % -16     == '-0x10'   (sign must NOT be lost)
    '%#x' % 0       == '0x0'     (zero also gets the prefix)
    '%#.4x' % 16    == '0x0010'  (precision pads digits, prefix kept)
    '%#08x' % -16   == '-0x00010' (zero-padding goes after '0x')
"""

# --- negative: sign comes first, then the prefix ---
assert '%#o' % -8 == '-0o10'
assert '%#.2o' % -8 == '-0o10'
assert '%#x' % -16 == '-0x10'
assert '%#X' % -16 == '-0X10'
assert '%#x' % -1 == '-0x1'
assert '%#X' % -255 == '-0XFF'

# --- zero also gets the prefix ---
assert '%#x' % 0 == '0x0'
assert '%#o' % 0 == '0o0'
assert '%#X' % 0 == '0X0'
assert '%#.2x' % 0 == '0x00'
assert '%#.3o' % 0 == '0o000'
assert '%#.0x' % 0 == '0x0'

# --- positive keeps working ---
assert '%#x' % 16 == '0x10'
assert '%#o' % 8 == '0o10'
assert '%#X' % 16 == '0X10'
assert '%#.4x' % 16 == '0x0010'
assert '%#.4o' % 8 == '0o0010'
assert '%#.4x' % -16 == '-0x0010'
assert '%.4x' % 16 == '0010'

# --- width / alignment ---
assert '%#8x' % -16 == '   -0x10'
assert '%#-8x' % -16 == '-0x10   '

# --- zero-padding: zeros go after sign and prefix ---
assert '%#05x' % -16 == '-0x10'
assert '%#06x' % -16 == '-0x010'
assert '%#06x' % 16 == '0x0010'
assert '%#08x' % -16 == '-0x00010'
assert '%#08x' % 16 == '0x000010'
assert '%#06x' % 0 == '0x0000'
assert '%#010x' % 0 == '0x00000000'
assert '%#010x' % -1 == '-0x0000001'
assert '%#07o' % -8 == '-0o0010'
assert '%#07o' % 8 == '0o00010'
assert '%#08o' % 0 == '0o000000'

print("test_percent_format_prefix_regression passed")
