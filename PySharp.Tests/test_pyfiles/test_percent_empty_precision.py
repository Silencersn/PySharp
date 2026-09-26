"""%-formatting treats an empty precision field ("%." specs) as precision 0 for every conversion type instead of raising "format requires a precision".

:kind: test
"""

# Regression: %-formatting treats an empty precision field as
# precision 0 (CPython sets the precision as soon as the dot is
# consumed) instead of raising "format requires a precision".
assert "%.g" % 123.456 == "1e+02"
assert "%.f" % 1.5 == "2"
assert "%.e" % 1234.5 == "1e+03"
assert "%.E" % 1234.5 == "1E+03"
assert "%.s" % "abcdef" == ""
assert "%.r" % "ab" == ""
assert "%.d" % 42 == "42"
assert "%.x" % 255 == "ff"
assert "%.o" % 8 == "10"
assert "%.g" % 0 == "0"
assert "%.g" % 5 == "5"

# neighboring faces stay intact
assert "%.3s" % "abcdef" == "abc"
assert "%.0f" % 1.5 == "2"
assert "%.*g" % (3, 123.456) == "123"

# a dot at the end of the spec is still an incomplete format
for spec in ["%.5", "%.3"]:
    try:
        spec % 3
        raise AssertionError("expected ValueError for " + repr(spec))
    except ValueError as e:
        assert str(e) == "incomplete format", (spec, str(e))

print("test_percent_empty_precision passed")
