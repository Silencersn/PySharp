# Regression test: round(x, ndigits) must keep subnormal magnitudes when
# ndigits matches the decimal order of the value (the composition 10**nd
# used to overflow the double range and collapse the result to -0.0), and
# a coarse-place rounding that overflows must raise OverflowError like
# CPython's double_round.
# Issue matrix: subnormal + boundary ndigits keeps the value
faces = [
    (-1e-323, 323, '-1e-323'),
    (1e-322, 322, '1e-322'),
    (-1e-322, 322, '-1e-322'),
    (1e-310, 310, '1e-310'),
    (-1e-310, 310, '-1e-310'),
    (-1.0000000000000002e-320, 320, '-1e-320'),
    (9.9e-324, 324, '1e-323'),
    (1e-323, 323, '1e-323'),
    (-1.2e-322, 322, '-1e-322'),
    (1.2e-322, 322, '1e-322'),
    (-9.999e-324, 324, '-1e-323'),
    (7.3e-320, 320, '7e-320'),
]
for x, nd, expected in faces:
    assert repr(round(x, nd)) == expected, (x, nd, repr(round(x, nd)))

# controls from the issue
assert repr(round(-1e-323, 322)) == '-0.0'
assert repr(round(-1e-323, 400)) == '-1e-323'
assert repr(round(-1.5e-8, 8)) == '-1e-08'
assert round(-2.5e-324) == 0
assert repr(round(2.675, 2)) == '2.67'
assert repr(round(2.5, 0)) == '2.0'
assert repr(round(2.5e-323, 323)) == '2e-323'
assert repr(round(5e-324, 323)) == '0.0'

# binary-exponent >= 52 makes the double an exact integer, so round is a
# no-op for every ndigits; the p+15 face is the sweep-crash regression case
assert float.fromhex('0x1.9030ca493c310p+15') == 51224.395089989644
assert repr(round(51224.395089989644, 0)) == '51224.0'
for nd in (100, 306, 308, 309, 323):
    assert repr(round(51224.395089989644, nd)) == '51224.395089989644', nd
intvals = [float.fromhex('0x1.5p+52'), 1.7976931348623157e308]
for x in intvals:
    for nd in (0, 100, 306, 308, 309, 323):
        assert round(x, nd) == x, (x, nd)

# ndigits beyond the clamp boundaries: > 323 returns x, < -308 returns -0.0
for x, nd in [(-1e-323, 324), (1.5, 10**6), (5e-324, 400)]:
    assert round(x, nd) == x, (x, nd)
assert repr(round(-1e-323, -400)) == '-0.0'
assert repr(round(-5e-324, -308)) == '-0.0'
assert repr(round(5e-324, -308)) == '0.0'
assert repr(round(99999.999, -2)) == '100000.0'
assert repr(round(123456789.0, -3)) == '123457000.0'

# rounding at a coarse place that overflows the double range raises
# OverflowError with CPython's message
for x, nd in [(1.7976931348623157e308, -307), (1.7976931348623157e308, -308),
              (-1.7976931348623157e308, -308), (1.5e308, -308)]:
    try:
        round(x, nd)
    except OverflowError as e:
        assert str(e) == 'rounded value too large to represent', (x, nd, e)
    else:
        raise AssertionError((x, nd))
assert repr(round(9.999999999999999e307, -308)) == '1e+308'

# deterministic sweep: random doubles via fromhex, round faces must be
# finite, sign-preserving, and integer-valued doubles stay exact
state = 7
digits = '0123456789abcdef'
for i in range(600):
    state = (state * 6364136223846793005 + 1442695040888963407) % (1 << 64)
    r1, r2 = state, (state * 6364136223846793005 + 1442695040888963407) % (1 << 64)
    state = r2
    mant = ''.join(digits[(r1 >> (4 * k)) & 0xF] for k in range(13))
    exp = (r2 >> 20) % 2097 - 1074  # stays inside the fromhex range
    sign = '-' if (r2 & 1) else ''
    x = float.fromhex(sign + '0x1.' + mant[1:] + 'p' + str(exp))
    nd = [0, 1, 5, 17, 100, 305, 308, 309, 310, 315, 320, 322, 323][(r2 >> 52) % 13]
    y = round(x, nd)
    if x == 0.0:
        assert repr(y) in ('0.0', '-0.0'), (x, nd, y)
    else:
        assert (y == 0.0) or (y < 0) == (x < 0), (x, nd, y)
    if exp >= 52:
        assert y == x, (x, nd, y)

print("test_float_round_subnormal_regression passed")
