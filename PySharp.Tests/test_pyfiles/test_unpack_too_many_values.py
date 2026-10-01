"""ValueError wording for unpacking assignments.

CPython 3.14 reference: _PyEval_UnpackIterable (Python/ceval.c) only appends
the got-count to "too many values to unpack" when the unpacked object is an
exact list, tuple or dict; every other iterable reports just the expected
count. "not enough values to unpack" always carries the got-count.

:kind: test
"""


def too_many(v):
    try:
        a, b = v
    except ValueError as e:
        return str(e)
    return "no error"


results = [
    too_many([1, 2, 3]),
    too_many((1, 2, 3)),
    too_many({1: 1, 2: 2, 3: 3}),
    too_many({1, 2, 3}),
    too_many(frozenset({1, 2, 3})),
    too_many("abc"),
    too_many(iter([1, 2, 3])),
    too_many(x for x in [1, 2, 3]),
]

expected = [
    "too many values to unpack (expected 2, got 3)",
    "too many values to unpack (expected 2, got 3)",
    "too many values to unpack (expected 2, got 3)",
    "too many values to unpack (expected 2)",
    "too many values to unpack (expected 2)",
    "too many values to unpack (expected 2)",
    "too many values to unpack (expected 2)",
    "too many values to unpack (expected 2)",
]

for got, want in zip(results, expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# not enough keeps the got-count in every form, including subclasses
class MyList(list):
    pass


try:
    a, b = MyList([1])
    assert False, "expected ValueError"
except ValueError as e:
    assert str(e) == "not enough values to unpack (expected 2, got 1)", str(e)

print("test_unpack_too_many_values passed")
