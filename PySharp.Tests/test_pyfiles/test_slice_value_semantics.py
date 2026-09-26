"""Verifies slice objects carry value semantics: rich comparison delegates to the (start, stop, step) tuple with an identity shortcut, the hash uses the tuplehash lanes without the length mix-in, repr prints the three-part constructor form, and indices() converts like CPython's _PySlice_GetLongIndices with clipping and exact error faces.

Value identity also makes slices usable as dict keys.

:kind: test
"""

# Regression: slice objects carry value semantics — rich comparison
# delegates to the tuple (start, stop, step) with an identity shortcut
# (so order comparisons work and member errors name the parts), the
# hash is the tuplehash lane combination without the length mix-in,
# repr prints the three-part constructor form, and slice.indices()
# converts like CPython's METH_O binding with _PySlice_GetLongIndices.

s = slice(1, 10, 2)

faces = [
    # value equality across the six comparison operators
    ("slice(1, 2) == slice(1, 2)", True),
    ("slice(1, 2) != slice(1, 2)", False),
    ("slice(1, 2) == slice(1, 3)", False),
    ("slice(1, 2, 3) == slice(1.0, 2.0, 3.0)", True),
    ("slice(1, 2) == 'x'", False),
    ("slice(1, 2) == (1, 2, None)", False),
    ("slice(1, 2) < slice(1, 3)", True),
    ("slice(1, 3) < slice(1, 2)", False),
    ("slice(1, 2) <= slice(1, 2)", True),
    ("slice(1, 2) > slice(0, 9)", True),
    ("slice(1, 2) >= slice(1, 2)", True),
    ("slice(1, 2) < 'x'", "TypeError: '<' not supported between instances of 'slice' and 'str'"),
    ("slice(3,) < slice(1, 2)", "TypeError: '<' not supported between instances of 'NoneType' and 'int'"),
    ("slice(0, 1, 1) >= slice(0, 1)", "TypeError: '>=' not supported between instances of 'int' and 'NoneType'"),
    # the hash lanes never collide with the equivalent tuple
    ("hash(slice(1, 2, 3)) == hash((1, 2, 3))", False),
    ("hash(slice(1, 2)) == hash(slice(1, 2))", True),
    ("hash(slice(1, 2, 3)) == hash(slice(1, 2.0, 3.0))", True),
    # constructor-form repr with the step made explicit
    ("repr(slice(1, 10, 2))", repr("slice(1, 10, 2)")),
    ("repr(slice(1, 10))", repr("slice(1, 10, None)")),
    ("repr(slice(None, None))", repr("slice(None, None, None)")),
    ("repr(slice(None, 5))", repr("slice(None, 5, None)")),
    ("repr(slice('a', 'b'))", repr("slice('a', 'b', None)")),
    ("str(slice(1, 2))", repr("slice(1, 2, None)")),
    # indices: normalization, clipping and error faces
    ("slice(1, 10, 2).indices(10)", "(1, 10, 2)"),
    ("slice(None).indices(10)", "(0, 10, 1)"),
    ("slice(5).indices(10)", "(0, 5, 1)"),
    ("slice(-3).indices(10)", "(0, 7, 1)"),
    ("slice(1, 10, 2).indices(9)", "(1, 9, 2)"),
    ("slice(10, 0, -1).indices(10)", "(9, 0, -1)"),
    ("slice(2**100, 2**200, 2).indices(10)", "(10, 10, 2)"),
    ("slice(-2**100, -2**100, -2).indices(10)", "(-1, -1, -2)"),
    ("slice(1, 2).indices(True)", "(1, 1, 1)"),
    ("slice(1, 2).indices(2**100)", "(1, 2, 1)"),
    ("slice(0, 10, 0).indices(10)", "ValueError: slice step cannot be zero"),
    ("slice(1, 2).indices(-5)", "ValueError: length should not be negative"),
    ("slice(1, 2).indices('a')", "TypeError: 'str' object cannot be interpreted as an integer"),
    ("slice(1, 2).indices()", "TypeError: slice.indices() takes exactly one argument (0 given)"),
    ("slice(1, 2).indices(1, 2)", "TypeError: slice.indices() takes exactly one argument (2 given)"),
    ("slice(1, 2).indices(bad=1)", "TypeError: slice.indices() takes no keyword arguments"),
    ("slice(1, 2).indices(1, bad=1)", "TypeError: slice.indices() takes no keyword arguments"),
    ("s.start", 1),
    ("s.stop", 10),
    ("s.step", 2),
]

for expr, expected in faces:
    try:
        got = repr(eval(expr))
    except Exception as e:
        got = type(e).__name__ + ": " + str(e)
    expected = expected if isinstance(expected, str) else repr(expected)
    assert got == expected, (expr, got, expected)

# the identity shortcut: same object compares per CPython's switch
assert (s < s) is False
assert (s <= s) is True
assert (s >= s) is True
assert (s == s) is True
assert (s != s) is False

# slices stay usable as dict keys with value identity
mapping = {slice(1, 2): "a", slice(1, 3): "b"}
assert mapping[slice(1, 2)] == "a"
assert len(mapping) == 2

print("slice value-semantics faces ok,", len(faces), "cases")
print("test_slice_value_semantics passed")
