"""Pins PEP 584 dict merging: dict | dict builds a merged copy (nb_or, right operand wins), d |= other updates in place through dict_update_arg semantics (exact dict, keys() mapping, or pairs iterable), and the __or__/__ror__/__ior__ wrappers exist on the type like CPython's add_operators output.

:kind: test
"""

# Regression: dict carried no nb_or / nb_inplace_or slots, so both forms
# raised "unsupported operand type(s) for |: 'dict' and 'dict'" while
# CPython 3.9+ merges (dict_or copies self then updates from other,
# dict_ior updates self through the update path and returns it).

# | builds a merged copy; the right operand's keys win
left = {1: "a", 2: "old"}
right = {2: "new", 3: "c"}
merged = left | right
assert merged == {1: "a", 2: "new", 3: "c"}
assert left == {1: "a", 2: "old"} and right == {2: "new", 3: "c"}

# the result of a plain merge is always a plain dict
assert type(merged) is dict

# chaining folds left to right
assert {1: 1} | {2: 2} | {3: 3} == {1: 1, 2: 2, 3: 3}
assert {1: 1, 2: 1} | {2: 2} | {1: 9} == {1: 9, 2: 2}

# a non-dict operand declines on either side
for bad in ([1], (1,), "ab", {1}, 3, None):
    try:
        left | bad
        raise AssertionError(f"expected TypeError for | with {bad!r}")
    except TypeError as e:
        assert str(e) == f"unsupported operand type(s) for |: 'dict' and '{type(bad).__name__}'", e
    try:
        bad | left
        raise AssertionError(f"expected TypeError for reversed | with {bad!r}")
    except TypeError:
        pass

# |= mutates and rebinds the same object
d = {1: "a"}
r = d
d |= {2: "b"}
assert d == {1: "a", 2: "b"} and r is d
d |= {2: "overwritten"}
assert d == {1: "a", 2: "overwritten"}

# dict_ior carries dict_update_arg semantics, not just exact dicts:
# a pairs iterable and a keys()-bearing mapping both merge
d = {1: "a"}
d |= [(2, "b"), (3, "c")]
assert d == {1: "a", 2: "b", 3: "c"}

class KeysMapping:
    def keys(self):
        return [2, 3]
    def __getitem__(self, key):
        return "from-mapping"

d |= KeysMapping()
assert d == {1: "a", 2: "from-mapping", 3: "from-mapping"}

# a broken source propagates its own error, nothing swallowed
try:
    d = {}
    d |= 3
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'int' object is not iterable" in str(e), e

# the operator wrappers are visible and behave like CPython's
assert hasattr(dict, "__or__") and hasattr(dict, "__ror__") and hasattr(dict, "__ior__")
assert hasattr({1: 2}, "__ior__")

d = {1: "a"}
assert d.__ior__({2: "b"}) is d and d == {1: "a", 2: "b"}
assert {1: 2}.__or__({3: 4}) == {1: 2, 3: 4}
# the reflected wrapper flips the pair: other | self
assert {1: 2}.__ror__({3: 4}) == {3: 4, 1: 2}

# subclasses participate on either side and still merge as plain dicts
class D(dict):
    pass

sub = D({9: "z"})
assert left | sub == {1: "a", 2: "old", 9: "z"}
assert sub | left == {9: "z", 1: "a", 2: "old"}
assert type(left | sub) is dict

sub |= {10: "w"}
assert sub == {9: "z", 10: "w"} and isinstance(sub, D)

# a subclass __or__ override wins over the inherited slot
class Overriding(dict):
    def __or__(self, other):
        return "overridden"

assert Overriding({}) | {1: 2} == "overridden"

print("test_dict_merge_operator passed")
