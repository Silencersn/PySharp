"""format() with the '%' presentation type appends the trailing percent sign to non-finite floats too, and the suffix counts toward width, fill and alignment while precision and grouping are ignored for inf/nan.

:kind: test
"""

# Regression test: format() with the '%' presentation type must append the
# trailing '%' suffix to non-finite floats too (CPython appends it
# unconditionally after rendering inf/nan, and the suffix counts as part of
# the text for width, fill and alignment).
inf = float("inf")
nan = float("nan")

# core matrix from the issue
assert format(inf, "%") == "inf%", format(inf, "%")
assert format(nan, "%") == "nan%", format(nan, "%")
assert format(-inf, "%") == "-inf%", format(-inf, "%")

# precision is ignored for non-finite values, the suffix still appears
assert format(inf, ".2%") == "inf%", format(inf, ".2%")
assert format(nan, ".0%") == "nan%", format(nan, ".0%")
assert format(inf, ".200%") == "inf%", format(inf, ".200%")

# grouping has no visible effect on inf/nan but the suffix stays
assert format(inf, ",%") == "inf%", format(inf, ",%")
assert format(nan, "_%") == "nan%", format(nan, "_%")

# the suffix is part of the text for width / fill / alignment
assert format(-inf, ">10%") == "     -inf%", format(-inf, ">10%")
assert format(inf, "<8%") == "inf%    ", format(inf, "<8%")
assert format(inf, "010%") == "000000inf%", format(inf, "010%")
assert format(-inf, "=010%") == "-00000inf%", format(-inf, "=010%")
assert format(inf, " %") == " inf%", format(inf, " %")
assert format(-inf, "+%") == "-inf%", format(-inf, "+%")

# alternate form on non-finite values still keeps the suffix
assert format(nan, "#%") == "nan%", format(nan, "#%")

# finite control faces (×100 + suffix, grouping, alternate form)
assert format(0.25, "%") == "25.000000%", format(0.25, "%")
assert format(0.5, ".1%") == "50.0%", format(0.5, ".1%")
assert format(2, "%") == "200.000000%", format(2, "%")
assert format(1234.5, ".3%") == "123450.000%", format(1234.5, ".3%")
assert format(1234567.0, ",%") == "123,456,700.000000%", format(1234567.0, ",%")
assert format(0.5, "#%") == "50.000000%", format(0.5, "#%")

# f-strings share the same path
assert f"{inf:%}" == "inf%", f"{inf:%}"
assert f"{nan:.2%}" == "nan%", f"{nan:.2%}"

print("test_float_format_percent_nonfinite passed")
