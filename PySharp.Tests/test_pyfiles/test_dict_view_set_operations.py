"""Pins the four set operations on dict_keys/dict_items views (dictviews_as_number): & | ^ - return a new set, accept any iterable on the right, reflect against a set on the left, and items ^ items takes CPython's key-indexed path that drops equal-valued pairs before any set add.

:kind: test
"""

# Regression: dict_keys/dict_items carried no nb_and/nb_or/nb_xor/
# nb_subtract slots, so every combination raised "unsupported operand
# type(s) for &: 'dict_keys' and ..." while the comparison protocol
# (< <= >= ==) already worked. CPython materializes the view into a set
# (dictviews_to_set) and folds the other operand in; & walks the other
# operand probing the view (_PyDictView_Intersect).

d = {"a": 1, "b": 2}
k = d.keys()
i = d.items()

# every operator returns a fresh plain set
assert k & k == {"a", "b"} and type(k & k) is set
assert k & {"a", "z"} == {"a"}
assert k | {1} == {1, "a", "b"}
assert k - {"a"} == {"b"}
assert k ^ {"a", "c"} == {"b", "c"}

# items views carry their pairs
assert i & {("a", 1)} == {("a", 1)}
assert i | {("c", 3)} == {("a", 1), ("b", 2), ("c", 3)}
assert i - {("a", 1)} == {("b", 2)}
assert i ^ {("a", 1), ("c", 3)} == {("b", 2), ("c", 3)}

# views compose with views, sets, and frozensets
assert k | d.copy().keys() == {"a", "b"}
assert k & frozenset({"a"}) == {"a"}
assert i & d.copy().items() == {("a", 1), ("b", 2)}

# any iterable works on the right, matching CPython's method delegation
assert k - ["a"] == {"b"}
assert k & ["a", "z"] == {"a"}
assert k | [1, "a"] == {1, "a", "b"}
assert k ^ ["a", "c"] == {"b", "c"}

# a non-iterable right operand raises the iterator error, not a TypeError
try:
    k & 1
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'int' object is not iterable" in str(e), e

# a set on the left reaches the view through the reflected slot; CPython's
# reflected try keeps the original order, so `-` subtracts the view from
# the LEFT operand's contents
assert {"a", "z"} & k == {"a"}
assert {1, "a"} | k == {1, "a", "b"}
assert {"a"} - k == set()
assert {"a", "c"} - k == {"c"}
assert frozenset({"a"}) - k == set()
assert {"a", "c"} ^ k == {"b", "c"}
assert {("a", 1)} & i == {("a", 1)}

# a non-iterable left operand fails materializing the left side
try:
    5 - k
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'int' object is not iterable" in str(e), e

# the reflected wrappers are visible on the view types
assert hasattr(type(k), "__rand__") and hasattr(type(k), "__ror__")
assert hasattr(type(k), "__sub__") and hasattr(type(k), "__rxor__")

# dict_values has no set operations at all
try:
    d.values() & {1}
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "unsupported operand type(s) for &: 'dict_values' and 'set'", e

# unhashable elements fail as set elements do
try:
    k | [[]]
    raise AssertionError("expected TypeError")
except TypeError:
    pass

# items ^ items with unhashable values: CPython's key-indexed path drops
# equal-valued pairs without hashing them, so mutual cancellation works
left = {"a": [1]}
right = {"a": [1]}
assert left.items() ^ right.items() == set()

# ...but a surviving pair with an unhashable value still fails at the add
try:
    {"a": [1]}.items() ^ {"b": [2]}.items()
    raise AssertionError("expected TypeError")
except TypeError:
    pass

# hashable values go through the ordinary symmetric difference
assert {"a": 1}.items() ^ {"a": 1, "b": 2}.items() == {("b", 2)}

# the comparison protocol still works alongside the operators
assert {"a"} < k and {"a", "b"} <= k and k >= {"a"}

print("test_dict_view_set_operations passed")
