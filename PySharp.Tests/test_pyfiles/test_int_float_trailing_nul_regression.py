"""
Regression: int()/float() string parsing must reject U+0000 anywhere in
the input. CPython's parsers only accept a literal when they consume the
whole string (Objects/longobject.c PyLong_FromUnicodeObject,
Python/pystrtod.c "No embedded NULs allowed",
Objects/floatobject.c float_from_string_inner), so an embedded or
trailing NUL is always invalid. The .NET numeric parsers silently take
a single trailing NUL as end-of-input instead, which made int('1\x00')
return 1 and float('1.5\x00') return 1.5.

The guard sits at the two parse entry points (BigIntegerHelper.TryParse
for every int(str)/int(bytes-like) path including the Nd transform and
base 0, TryParseFloatString for float(str) including the underscore
stripping), so the error messages below quote the original argument.
"""

def err(fn):
    try:
        return ('ok', fn())
    except Exception as e:
        return (type(e).__name__, str(e))

N = chr(0)

# trailing NUL on every int() entry: str, base 0, bytes-like
assert err(lambda: int('1' + N)) == ('ValueError', "invalid literal for int() with base 10: '1\\x00'")
assert err(lambda: int('12' + N)) == ('ValueError', "invalid literal for int() with base 10: '12\\x00'")
assert err(lambda: int('-1' + N)) == ('ValueError', "invalid literal for int() with base 10: '-1\\x00'")
assert err(lambda: int(' 1' + N)) == ('ValueError', "invalid literal for int() with base 10: ' 1\\x00'")
assert err(lambda: int('1' + N, 10)) == ('ValueError', "invalid literal for int() with base 10: '1\\x00'")
assert err(lambda: int('1' + N, 0)) == ('ValueError', "invalid literal for int() with base 0: '1\\x00'")
# a trailing space after the NUL used to be trimmed away, leaving the NUL trailing
assert err(lambda: int('1' + N + ' ')) == ('ValueError', "invalid literal for int() with base 10: '1\\x00 '")
assert err(lambda: int(b'1\x00')) == ('ValueError', "invalid literal for int() with base 10: b'1\\x00'")
assert err(lambda: int(bytearray(b'1\x00'))) == ('ValueError', "invalid literal for int() with base 10: b'1\\x00'")
assert err(lambda: int(memoryview(b'1\x00'))) == ('ValueError', "invalid literal for int() with base 10: b'1\\x00'")

# the Nd transform passes NUL through: full-width digits must not parse
assert err(lambda: int('０１' + N)) == ('ValueError', "invalid literal for int() with base 10: '０１\\x00'")

# trailing NUL on every float() entry: plain, exponent, underscore-stripped
assert err(lambda: float('1' + N)) == ('ValueError', "could not convert string to float: '1\\x00'")
assert err(lambda: float('1.5' + N)) == ('ValueError', "could not convert string to float: '1.5\\x00'")
assert err(lambda: float('1e5' + N)) == ('ValueError', "could not convert string to float: '1e5\\x00'")
assert err(lambda: float('1_0' + N)) == ('ValueError', "could not convert string to float: '1_0\\x00'")
assert err(lambda: float('1' + N + ' ')) == ('ValueError', "could not convert string to float: '1\\x00 '")

# NUL was never valid anywhere else — these keep failing the same way
assert err(lambda: int(N)) == ('ValueError', "invalid literal for int() with base 10: '\\x00'")
assert err(lambda: int(N + '1')) == ('ValueError', "invalid literal for int() with base 10: '\\x001'")
assert err(lambda: int('1' + N + '2')) == ('ValueError', "invalid literal for int() with base 10: '1\\x002'")
assert err(lambda: int('1' + N, 16)) == ('ValueError', "invalid literal for int() with base 16: '1\\x00'")
assert err(lambda: int('1' + N, 2)) == ('ValueError', "invalid literal for int() with base 2: '1\\x00'")
assert err(lambda: float('1' + N + '2')) == ('ValueError', "could not convert string to float: '1\\x002'")
assert err(lambda: complex('1' + N)) == ('ValueError', 'complex() arg is a malformed string')
assert err(lambda: float.fromhex('0x1' + N))[0] == 'ValueError'

# valid literals in the same shapes keep parsing
assert int('10') == 10 and int(' 10 ') == 10 and int('１０') == 10
assert int(b'10') == 10 and int(b' 10 ') == 10 and int(b'1_0') == 10
assert int(memoryview(b'42')) == 42
assert float('1.5') == 1.5 and float(' 1.5 ') == 1.5 and float('1_0') == 10.0
assert float('inf') == float('inf') and float('nan') != float('nan')

print("test_int_float_trailing_nul_regression passed")
