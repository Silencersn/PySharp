"""float.fromhex parses subnormal hex strings with IEEE 754 subnormal semantics instead of underflowing to zero, and the hex-to-fromhex round trip stays an exact identity down to the smallest subnormal.

:kind: test
"""

# Regression test: float.fromhex parses subnormal hex strings with IEEE 754
# subnormal semantics instead of underflowing to zero — the hex→fromhex
# round trip stays an exact identity down to the smallest subnormal.
faces = [
    ('0x0.0000000000001p-1022', 5e-324),
    ('0x1p-1074', 5e-324),
    ('-0x1p-1074', -5e-324),
    ('0x1.8p-1023', 1.668805393880401e-308),
    ('-0x1.8p-1023', -1.668805393880401e-308),
    ('0x0.0000000000002p-1022', 1e-323),
    ('0x1p-1023', 1.1125369292536007e-308),
    ('0x0.0000000000000fp-1022', 5e-324),
    ('0x0.00000000000017p-1022', 5e-324),
    ('0x0.00000000000018p-1022', 1e-323),
    ('0x1p-1022', 2.2250738585072014e-308),
]
for s, expected in faces:
    v = float.fromhex(s)
    assert v == expected, (s, v, expected)
    assert float.fromhex(v.hex()) == v, (s, v.hex())

# hex→fromhex round trip on the canonical spellings
assert (5e-324).hex() == '0x0.0000000000001p-1022'
assert (-0.0).hex() == '-0x0.0p+0'
assert float.fromhex('-0x0.0p+0') == -0.0

# deterministic sweep over the subnormal range: fromhex must invert hex()
state = 12345
digits = '0123456789abcdef'
for i in range(800):
    state = (state * 6364136223846793005 + 1442695040888963407) % (1 << 64)
    r = state
    ndig = 1 + (r >> 8) % 13
    mant = ''.join(digits[(r >> (4 * k + 12)) & 0xF] for k in range(ndig))
    mant = mant.lstrip('0') or '0'
    exp = -(1000 + (r >> 20) % 75)
    sign = '-' if (r & 1) else ''
    frac = len(mant) > 1 and (r >> 40) & 1
    s = sign + '0x' + ('0.' + mant if frac else mant) + 'p' + str(exp)
    x = float.fromhex(s)
    assert float.fromhex(x.hex()) == x, (s, x, x.hex())

print("test_float_fromhex_subnormal passed")
