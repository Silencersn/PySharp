"""
BigInteger -> double conversion must round to nearest, ties to
even (CPython's PyLong_AsDouble), using guard + sticky bits. The plain .NET
(double) cast truncates the bits below the 53-bit mantissa, landing 1 ulp
low at rounding boundaries such as float(10**30).

Expected hex values verified against CPython 3.14.

CPython 3.14 reference:
    float(10**30)   -> 0x1.93e5939a08ceap+99  (== 1e+30; truncation gave 1 ulp low)
    float(2**53+1)  -> 0x1.0000000000000p+53  (exact tie rounds to even)
    float(2**53+3)  -> 0x1.0000000000002p+53  (tie rounds up to even)
    float(2**54+2)  -> 0x1.0000000000000p+54  (exact tie rounds to even)
    float(2**54+3)  -> 0x1.0000000000001p+54
    float(2**1023 + 2**969 - 2**968) -> 0x1.0000000000000p+1023 (sticky bit)

:kind: test
"""

# rounding boundaries (10**k values were 1 ulp low before the fix)
assert float(10**30).hex() == '0x1.93e5939a08ceap+99'
assert float(-(10**30)).hex() == '-0x1.93e5939a08ceap+99'
assert float(10**40).hex() == '0x1.d6329f1c35ca5p+132'
assert float(-(10**40)).hex() == '-0x1.d6329f1c35ca5p+132'
assert float(10**308).hex() == '0x1.1ccf385ebc8a0p+1023'

# half-way ties must round to even, not truncate
assert float(2**53 + 1).hex() == '0x1.0000000000000p+53'
assert float(2**53 + 2).hex() == '0x1.0000000000001p+53'
assert float(2**53 + 3).hex() == '0x1.0000000000002p+53'
assert float(2**54 + 1).hex() == '0x1.0000000000000p+54'
assert float(2**54 + 2).hex() == '0x1.0000000000000p+54'
assert float(2**54 + 3).hex() == '0x1.0000000000001p+54'
assert float(2**1023 + 2**969 - 2**968).hex() == '0x1.0000000000000p+1023'

# previously-correct samples stay correct
assert float(3**40).hex() == '0x1.517168a4523fdp+63'
assert float(7**30).hex() == '0x1.2a4e415e1e1b3p+84'
assert float(2**61 + 1).hex() == '0x1.0000000000000p+61'
assert float(10**17 + 1).hex() == '0x1.6345785d8a000p+56'
assert float(2**1023).hex() == '0x1.0000000000000p+1023'

# the same conversion backs arithmetic paths
assert 10**30 * 1.0 == 1e30
assert 10**30 + 0.0 == 1e30
assert 10**30 / 1.0 == 1e30
assert (10**30) ** 1.0 == 1e30
assert float("1e30") == 1e30

# too-large ints still raise instead of silently becoming inf
try:
    float(10**400)
except OverflowError:
    pass
else:
    raise AssertionError('float(10**400) must raise OverflowError')

print("test_int_float_conversion_rounding passed")
