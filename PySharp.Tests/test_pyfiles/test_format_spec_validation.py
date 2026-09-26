"""
format() must reject invalid presentation types and invalid
flag/type combinations with CPython's messages instead of silently
accepting them. Covers:
- the 'z' (negative zero coercion) option: rejected for integers,
  applied for floats (clamping -0.0) including the omitted type
- an omitted presentation type on floats: repr rendering without a
  precision, 'g'-with-add-dot-0 semantics with one (exponent threshold
  one stricter than plain 'g', trailing zeros stripped, ".0" appended)
- precision rejected for integer presentations
- unknown single-character types and doubled grouping separators

:kind: test
"""

# z option: rejected for integers and bool
for value in (1, True, -1):
    for spec in ('z', '-z', '+z', 'zd', 'z,'):
        try:
            format(value, spec)
        except ValueError as e:
            assert str(e) == "Negative zero coercion (z) not allowed in integer format specifier", (value, spec, str(e))
        else:
            raise AssertionError(f"z option accepted for {value!r} with {spec!r}")

# z option: floats clamp negative zero
assert format(-0.0, 'z') == '0.0'
assert format(-0.0, 'zg') == '0'
assert format(-0.0, 'zf') == '0.000000'
assert format(-0.0, 'z5g') == '    0'
assert format(-0.0, 'z10') == '       0.0'
assert format(-0.0, '-z') == '0.0'
assert format(1.0, 'z') == '1.0'
assert format(1.0, 'zg') == '1'
assert format(-1.5, 'z10') == '      -1.5'

# omitted float type: repr rendering (with width/grouping applied)
assert format(1.0, '10') == '       1.0'
assert format(-0.0, '10') == '      -0.0'
assert format(-5.0, '10') == '      -5.0'
assert format(1.0, '<8') == '1.0     '
assert format(1.0, '_') == '1.0'
assert format(123456.789, ',') == '123,456.789'
assert format(1e20, ',') == '1e+20'
assert format(1.0, '10,') == '       1.0'

# omitted float type with precision: 'g' + add-dot-0
assert format(1.0, '.2') == '1.0'
assert format(1.5, '.2') == '1.5'
assert format(12.0, '.2') == '1.2e+01'
assert format(10.0, '.2') == '1e+01'
assert format(100.0, '.2') == '1e+02'
assert format(123456.0, ',.2') == '1.2e+05'
assert format(0.0, '.2') == '0.0'
assert format(0.05, '.2') == '0.05'
assert format(-0.0, 'z.2') == '0.0'

# precision rejected for integer presentations
for spec in ('.2d', '.2b', '.2o', '.2x', '.2c', '.2n', '.0d', '10.5d'):
    try:
        format(1, spec)
    except ValueError as e:
        assert str(e) == "Precision not allowed in integer format specifier", (spec, str(e))
    else:
        raise AssertionError(f"precision accepted for {spec!r}")

# float-style codes still accept precision for ints
assert format(1, '.2e') == '1.00e+00'
assert format(1, '.2f') == '1.00'
assert format(1, '.2%') == '100.00%'

# unknown presentation types (single leftover character)
for spec in ('q', 'j', 'v', 's', '10z', '.2z', '#z'):
    try:
        format(1, spec)
    except ValueError as e:
        expected = spec[-1] if spec[-1] not in ',_' else spec[0]
        assert str(e) == f"Unknown format code '{expected}' for object of type 'int'", (spec, str(e))
    else:
        raise AssertionError(f"unknown type accepted: {spec!r}")
try:
    format(1.0, 'b')
except ValueError as e:
    assert str(e) == "Unknown format code 'b' for object of type 'float'"
else:
    raise AssertionError("float accepted 'b'")

# multi-character garbage stays "Invalid format specifier"
for spec in ('sz', 'dz', 'fz', 'xd', '.2zg'):
    try:
        format(1, spec)
    except ValueError as e:
        assert str(e) == f"Invalid format specifier '{spec}' for object of type 'int'", (spec, str(e))
    else:
        raise AssertionError(f"multi-char garbage accepted: {spec!r}")

# grouping/type compatibility after the parse
try:
    format(1.0, ',b')
except ValueError as e:
    assert str(e) == "Cannot specify ',' with 'b'."
else:
    raise AssertionError("float accepted ',b'")
try:
    format(1.0, ',x')
except ValueError as e:
    assert str(e) == "Cannot specify ',' with 'x'."
else:
    raise AssertionError("float accepted ',x'")
assert format(1, '_b') == '1'
assert format(1, '_d') == '1'

# two DIFFERENT separators in one position conflict
for spec in ('_,', ',_', '_,_', '.2_,', '_,x'):
    try:
        format(1, spec)
    except ValueError as e:
        assert str(e) == "Cannot specify both ',' and '_'.", (spec, str(e))
    else:
        raise AssertionError(f"doubled grouping accepted: {spec!r}")

# a repeated SAME separator is not consumed by the grouping parse
# (formatter_unicode.c reads ',' once, '_' once; the last check peeks
# without consuming) — it lands in the type field, so int/float/str
# report the grouping-vs-type error or unknown-code messages instead
for obj, spec, msg in [
    (1, '__', "Cannot specify '_' with '_'."),
    (1, ',,', "Cannot specify ',' with ','."),
    (1, '10__', "Cannot specify '_' with '_'."),
    (1.5, '__', "Cannot specify '_' with '_'."),
    ('a', '__', "Cannot specify '_' with '_'."),
    (1, '.2,,', "Unknown format code ',' for object of type 'int'"),
    (1.5, '.__', "Unknown format code '_' for object of type 'float'"),
    (1, '__,', "Invalid format specifier '__,' for object of type 'int'"),
]:
    try:
        format(obj, spec)
    except ValueError as e:
        assert str(e) == msg, (repr(obj), spec, str(e))
    else:
        raise AssertionError(f"repeated grouping accepted: {repr(obj)} {spec!r}")

# a width grouping and a precision grouping may coexist (independent flags)
assert format(1234.5, ',.6_') == '1,234.5'

# str keeps its own messages (z / unknown code)
try:
    format('a', 'z')
except ValueError as e:
    assert str(e) == "Negative zero coercion (z) not allowed in string format specifier"
try:
    format('a', 'q')
except ValueError as e:
    assert str(e) == "Unknown format code 'q' for object of type 'str'"

# types without __format__ reject non-empty specs with TypeError
try:
    format(b'a', 'q')
except TypeError as e:
    assert str(e) == "unsupported format string passed to bytes.__format__"
else:
    raise AssertionError("bytes accepted a spec without TypeError")

# f-string paths share the slots
f = 1.0
assert f"{f}" == '1.0'
assert f"{-0.0}" == '-0.0'
assert f"{f:g}" == '1'
assert f"{10**20:d}" == '100000000000000000000'

print("test_format_spec_validation passed")
