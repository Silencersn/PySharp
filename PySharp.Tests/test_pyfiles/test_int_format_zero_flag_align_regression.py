# Regression: the '0' format-spec flag supplies fill '0' even when an
# explicit alignment is present (CPython parses "<04" as fill '0' with
# align '<'); it only defaults the alignment to '=' when none was
# parsed. Non-'=' alignments pad the sign+digits text plainly.
cases = [
    (5, "<04", "5000"),
    (5, ">04", "0005"),
    (5, "^04", "0500"),
    (-5, "<04", "-500"),
    (-5, ">04", "00-5"),
    (-5, "^04", "0-50"),
    (5, "<+04", "+500"),
    (42, "<06x", "2a0000"),
    (5, "<06b", "101000"),
    (5, "04", "0005"),
    (5, "0<4", "5000"),
    (12, "=06d", "000012"),
    (-12, "=06d", "-00012"),
    (12, "=+06d", "+00012"),
    (12, "0=6d", "000012"),
    (12, "0=+6d", "+00012"),
    (-5, "=8", "-      5"),
    (5, "^05", "00500"),
    (5, "<8", "5       "),
    (5, "*>04", "***5"),
    (1.5, "<08", "1.500000"),
    (-1.5, "<08", "-1.50000"),
    (-5.25, "=8", "-   5.25"),
]
for value, spec, expect in cases:
    result = format(value, spec)
    assert result == expect, (value, spec, result, expect)

assert f"{5:<04}" == "5000"
assert f"{5:>04}" == "0005"
assert f"{5:^04}" == "0500"
assert f"{-5:=06}" == "-00005"

# 'ab'.ljust-style str fill is unaffected; str with explicit zero fill works
assert format("ab", "0>4") == "00ab"

print("test_int_format_zero_flag_align_regression passed")
