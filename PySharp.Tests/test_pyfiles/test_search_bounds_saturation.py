"""str/list/tuple search-method start and end bounds saturate for huge magnitudes instead of overflowing, matching CPython's Py_ssize_t conversion.

:kind: test
"""

# Regression: str/list/tuple search-method bounds saturate instead of
# overflowing. CPython converts start/end through Py_ssize_t, so any
# magnitude is valid: huge negative values wrap by len and clamp to 0,
# huge positive values simply fall past the end.
HB = -10**10
HP = 10**10
HUGE = 10**100

assert "abc".index("a", HB) == 0
assert "abc".rfind("c", HB) == 2
assert "abc".rindex("c", HB) == 2
assert "abc".count("a", HB) == 1
assert "abc".startswith("a", HB)
assert "abc".endswith("c", HB)
assert "abc".find("b", HB, 10) == 1

# a huge negative end clamps to 0, so the window is empty and the
# search legitimately fails with ValueError
try:
    "abc".index("a", 0, HB)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "substring not found"

# magnitudes beyond ssize_t saturate the same way
assert "abc".index("a", -HUGE) == 0
assert "abc".startswith("a", -HUGE)
assert "abc".find("a", 0, HUGE) == 0
assert "abc".find("a", HP) == -1
assert "abc".count("", HP) == 0
assert "abc".rfind("b", 0, HB) == -1
try:
    "abc".index("a", HUGE)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "substring not found"

# list / tuple index shares the saturation
assert [1, 2].index(2, HB) == 1
assert (1, 2).index(2, HB) == 1
assert [1, 2].index(2, HB, HP) == 1
assert [1, 2].index(1, -HUGE) == 0
try:
    [1, 2].index(5, HB)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "list.index(x): x not in list"
try:
    [1, 2].index(1, HUGE)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "list.index(x): x not in list"
try:
    (1, 2).index(1, 0, HB)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "tuple.index(x): x not in tuple"

# medium negative bounds keep the classic wrap behavior
assert "hello world".find("world", -4) == -1
assert "hello world".find("world", -11) == 6
assert [1, 2, 3].index(3, -2) == 2

print("test_search_bounds_saturation passed")
