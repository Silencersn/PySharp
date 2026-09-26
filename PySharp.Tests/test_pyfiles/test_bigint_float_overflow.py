"""Converting a bigint outside the double range to float raises OverflowError with CPython's 'int too large to convert to float' message at every conversion site — mixed arithmetic, int pow, complex operations, the complex() constructor and float formatting — while comparisons never convert.

:kind: test
"""

# Regression: arithmetic conversions of a bigint to float follow
# PyLong_AsDouble — a value outside the double range (including one
# that rounds up to infinity) raises "int too large to convert to
# float" instead of silently producing inf/0.0. Conversions feed the
# mixed int/float and int/complex operations, negative-exponent int
# pow, the complex() constructor and the %e/%f/%g/% format fallback.

def raises_overflow(src, expected="int too large to convert to float"):
    try:
        exec(src, globals())
    except OverflowError as e:
        assert str(e) == expected, (src, str(e), expected)
    else:
        raise AssertionError("expected OverflowError for " + src)

# every mixed int/float arithmetic operation converts first
for src in [
    "r = 10**400 * 1.0",
    "r = 1.0 * 10**400",
    "r = 10**400 + 1.0",
    "r = 10**400 - 1.0",
    "r = 1.0 - 10**400",
    "r = 10**400 / 1.0",
    "r = 10**400 // 1.0",
    "r = 10**400 % 1.0",
    "r = 1.0 % 10**400",
    "r = divmod(10**400, 1.0)",
    "r = divmod(1.0, 10**400)",
    "r = (-10)**400 * 1.0",
    "r = 10**400 * 1.5",
]:
    raises_overflow(src)

# int pow converts to float for negative and float exponents
raises_overflow("r = (10**400) ** -1")
raises_overflow("r = pow(10**400, -1)")
raises_overflow("r = (10**400) ** 0.5")
raises_overflow("r = 2.0 ** 10**400")

# complex arithmetic and pow convert the int operand too
raises_overflow("r = complex(1,2) * 10**400")
raises_overflow("r = complex(1,2) + 10**400")
raises_overflow("r = complex(1,2) ** 10**400")

# the complex() constructor and the float-format fallback convert
raises_overflow("r = complex(10**400)")
raises_overflow("r = complex(1.0, 10**400)")
raises_overflow("r = format(10**400, '.3e')")
raises_overflow("r = '{:.3f}'.format(10**400)")

# the double range boundary uses the rounded value: exactly MaxValue
# converts, a value that ties or exceeds rounds to infinity and raises
m = (2**1024 - 2**971) * 1.0
assert m == 1.7976931348623157e+308
raises_overflow("r = (2**1024) * 1.0")
raises_overflow("r = (2**1024 - 2**970) * 1.0")

# int/int true division keeps its own overflow message
raises_overflow("r = 10**400 / 2", "integer division result too large for a float")

# conversions are already covered: float(10**400) raises
raises_overflow("r = float(10**400)")

# comparisons never convert: no overflow, no false equality with inf
assert (10**400 == float("inf")) is False
assert (complex(1, 2) == 10**400) is False

# representable operands keep working: a small int underflows its
# float result to 0.0 legitimately, and int ** int stays exact
assert 10**-400 * 1.0 == 0.0
assert (10**400) ** 1 == 10**400
assert 2.0 ** 3 == 8.0

print("test_bigint_float_overflow passed")
