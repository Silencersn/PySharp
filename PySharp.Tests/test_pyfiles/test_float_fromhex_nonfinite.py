"""float.fromhex parses optionally signed, case-insensitive inf/infinity/nan literals before the hex grammar, rejects partial tokens with ValueError, and keeps the hex() round trip intact for non-finite values.

:kind: test
"""

# Regression: float.fromhex parses non-finite literals like CPython's
# _Py_parse_inf_or_nan — an optionally signed inf/infinity/nan token,
# ASCII case-insensitive, checked before the hex grammar; trailing
# content still fails the literal check, so the hex() round trip
# survives infinity and NaN.

CASES = [
    ("inf", float("inf")),
    ("-inf", float("-inf")),
    ("+inf", float("inf")),
    ("nan", float("nan")),
    ("-nan", float("nan")),
    ("+NAN", float("nan")),
    ("INFINITY", float("inf")),
    ("Infinity", float("inf")),
    ("NaN", float("nan")),
    ("iNf", float("inf")),
    (" -Inf ", float("-inf")),
    ("\tINF\n", float("inf")),
    ("infinity", float("inf")),
    ("INFiniTY", float("inf")),
    ("  nan  ", float("nan")),
]
for source, expected in CASES:
    value = float.fromhex(source)
    if expected != expected:
        # NaN: compare by inequality, repr is 'nan' either way
        assert value != value and repr(value) == "nan", (source, value)
    else:
        assert value == expected and repr(value) == repr(expected), (source, value)

# a partial token is not a literal: the trailing residue fails
for source in ["infin", "infx", "nan(1)", "nan x", "inf2", "infinityy", "nanp", "i"]:
    try:
        float.fromhex(source)
        raise AssertionError("expected ValueError for " + source)
    except ValueError as e:
        assert str(e) == "invalid hexadecimal floating-point string", (source, e)

# the hex() round trip holds for the non-finite values
assert float.fromhex(float("inf").hex()) == float("inf")
assert float.fromhex(float("-inf").hex()) == float("-inf")
nan_back = float.fromhex(float("nan").hex())
assert nan_back != nan_back and repr(nan_back) == "nan"

# finite hex faces are untouched
assert float.fromhex("0x1p2") == 4.0
assert float.fromhex("0x1.8") == 1.5

print("test_float_fromhex_nonfinite passed")
