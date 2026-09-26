"""
PEP 515 underscore digit separators must work in every numeric
literal form — float mantissa/fraction/exponent, complex literals, and hex/
binary/octal ints — instead of crashing the process with an unhandled
FormatException (double.Parse used to receive an empty string because the
underscore stripping replaced within a cleared builder). Invalid underscore
placements keep CPython's tokenize-time SyntaxError.

CPython 3.14.6 reference:
    round(1_000.5)        -> 1000
    1_0.5                 -> 10.5
    1e2_0                 -> 1e+20
    .5_5                  -> 0.55
    1_000.5j              -> 1000.5j
    1_0_0.1_0_1           -> 100.101
    0x_10                 -> 16
    1__000                -> SyntaxError: invalid decimal literal
    0x10_                 -> SyntaxError: invalid hexadecimal literal
    0x18p0                -> SyntaxError: invalid hexadecimal literal

:kind: test
"""

assert round(1_000.5) == 1000
assert 1_0.5 == 10.5
assert [1_000.5, 2_0.5] == [1000.5, 20.5]
assert 1_000e5 == 100000000.0
assert 1_0e2 == 1000.0
assert 1e2_0 == 1e+20
assert 1_0.5e2 == 1050.0
assert .5_5 == 0.55
assert 1_000.5j == 1000.5j
assert 1_000j == 1000j
assert 1_0_0.1_0_1 == 100.101
assert 0x_10 == 16
assert 0x10_20 == 4128
assert 0b1_0_1 == 5
assert 0o7_7 == 63
assert 1_000 == 1000
assert 1_000_000.000_001 == 1000000.000001


def default_arg(v=1_000.5):
    return v


assert default_arg() == 1000.5


def expect_syntax_error(src, fragment):
    try:
        exec(src)
    except SyntaxError as e:
        message = getattr(e, 'msg', None) or str(e)
        assert fragment in message, message
    else:
        raise AssertionError('expected SyntaxError: ' + src)


# invalid underscore placements are tokenize-time errors (CPython parity)
expect_syntax_error('x = 1__000', 'invalid decimal literal')
expect_syntax_error('x = 1000_', 'invalid decimal literal')
expect_syntax_error('x = 1000_.5', 'invalid decimal literal')
expect_syntax_error('x = 1e2_', 'invalid decimal literal')
expect_syntax_error('x = 1_e2', 'invalid decimal literal')
expect_syntax_error('x = 0x10_', 'invalid hexadecimal literal')
expect_syntax_error('x = 1_000_', 'invalid decimal literal')

# hexadecimal float literals are not Python literals at all
expect_syntax_error('x = 0x18p0', 'invalid hexadecimal literal')
expect_syntax_error('x = 0x1_8p0', 'invalid hexadecimal literal')

print("test_numeric_underscore_literal passed")
