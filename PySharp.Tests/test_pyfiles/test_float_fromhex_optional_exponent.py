"""float.fromhex follows CPython's grammar with the 0x prefix, fraction, p-exponent and surrounding whitespace all optional, rounds half-to-even from the coefficient, raises OverflowError for out-of-range exponents and ValueError for parse failures.

:kind: test
"""

# Regression: float.fromhex follows CPython's float_fromhex grammar —
# the 0x prefix, the fraction, the (p|P) exponent and the surrounding
# whitespace are all optional (a missing exponent means p0, and 'e'
# stays a hex digit); values round half-to-even from the coefficient
# with the exponent applied as powers of two, and out-of-range
# exponents raise OverflowError while parse failures raise ValueError.

CASES = [
    # the issue's matrix: no p exponent required
    ("10", 16.0), ("0x10", 16.0), ("1.8", 1.5), ("0x1.8", 1.5),
    ("-10", -16.0), ("1e2", 482.0), ("ff", 255.0), ("0X10", 16.0),
    (" 0x10 ", 16.0),
    # p exponent faces keep working, in both cases and with signs
    ("0x1p2", 4.0), ("0x1P2", 4.0), ("-0x1.8p2", -6.0), ("0x1p-1", 0.5),
    ("+0x10", 16.0),
    # zero keeps its sign; odd but legal grammars
    ("0x0", 0.0), ("-0x0", -0.0), ("f.", 15.0), ("0.p1", 0.0),
    ("f.p1", 30.0), ("0x10.", 16.0), (".5", 0.3125), ("0x.8p1", 1.0),
    # coefficient rounding is half-to-even over the full coefficient
    ("0x1.0000000000000ap0", 1.0000000000000002),
    ("0x1fffffffffffff0p-4", 9007199254740991.0),
    # representability limits: subnormals, underflow to zero
    ("0x1p1023", 8.98846567431158e+307),
    ("0x1.fffffffffffffp1023", 1.7976931348623157e+308),
    ("0x1p-1074", 5e-324),
    ("0x0.0000000000001p-1022", 5e-324),
    ("0x1p-1100", 0.0),
    # strtol-style exponent clamping: a huge negative exponent
    # underflows to zero (a huge positive one overflows, below)
    ("0x1p-99999999999999999999", 0.0),
    # long coefficients round like CPython
    ("123456789abcdef12345p-70", 72.81777777777778),
    # whitespace is ASCII-only and only at the ends
    ("\t0x10\n", 16.0),
]
for source, expected in CASES:
    value = float.fromhex(source)
    if isinstance(expected, str):
        raise AssertionError(f"{source!r}: expected {expected}, got {value!r}")
    assert value == expected and repr(value) == repr(expected), (source, value, expected)

# out-of-range exponents raise OverflowError; a strtol-clamped huge
# positive exponent lands in the same overflow path
for source in ["0x1p1024", "0x1p1025", "0x1.ffffffffffffffffp1023", "0x1p99999999999999999999"]:
    try:
        float.fromhex(source)
        raise AssertionError("expected OverflowError for " + source)
    except OverflowError as e:
        assert str(e) == "hexadecimal value too large to represent as a float", (source, e)

# parse failures raise the CPython ValueError
for source in ["0x10 x", "0x", "0x1.p", "0x1p", "0x1p+", "", "  ", "0xg", "1p2x"]:
    try:
        float.fromhex(source)
        raise AssertionError("expected ValueError for " + source)
    except ValueError as e:
        assert str(e) == "invalid hexadecimal floating-point string", (source, e)

# a non-string argument reports the built-in conversion failure
try:
    float.fromhex(1)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "bad argument type for built-in operation", e

print("test_float_fromhex_optional_exponent passed")
