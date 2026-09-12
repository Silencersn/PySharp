"""
Regression: reversed() must support dicts and their views with dedicated
reverse iterators (CPython dict_reversed). reversed(dict) previously fell
through to the len/getitem protocol and raised KeyError because dict
subscripts are key lookups.
"""

d = {"a": 1, "b": 2, "c": 3}

# iterator type names match CPython
assert type(reversed(d)).__name__ == "dict_reversekeyiterator"
assert type(reversed(d.keys())).__name__ == "dict_reversekeyiterator"
assert type(reversed(d.items())).__name__ == "dict_reverseitemiterator"
assert type(reversed(d.values())).__name__ == "dict_reversevalueiterator"

# reverse order over keys, items and values
assert list(reversed(d)) == ["c", "b", "a"]
assert list(reversed(d.keys())) == ["c", "b", "a"]
assert list(reversed(d.items())) == [("c", 3), ("b", 2), ("a", 1)]
assert list(reversed(d.values())) == [3, 2, 1]

# partial consumption keeps the remaining reverse order
rk = reversed(d)
next(rk)
assert list(rk) == ["b", "a"]

# iterators are themselves iterable but not reversible
it = reversed(d)
assert list(it) == ["c", "b", "a"]
try:
    reversed(it)
except TypeError as e:
    assert "'dict_reversekeyiterator' object is not reversible" in str(e)
else:
    raise AssertionError("reverse iterator accepted by reversed()")

# mutation during reversed iteration raises like forward iteration
d2 = {"a": 1, "b": 2}
it2 = reversed(d2)
next(it2)
d2["c"] = 3
try:
    next(it2)
except RuntimeError as e:
    assert str(e) == "dictionary changed size during iteration"
else:
    raise AssertionError("mutation during reversed iteration silent")

# empty dicts and views
assert list(reversed({})) == []
assert list(reversed({}.keys())) == []
assert list(reversed({}.items())) == []
assert list(reversed({}.values())) == []

# single-entry and larger dicts
d3 = {}
d3["x"] = 1
assert list(reversed(d3)) == ["x"]
big = {i: i * 2 for i in range(20)}
rb = list(reversed(big))
assert rb == list(range(19, -1, -1))
assert list(reversed(big.values()))[0] == 38
assert list(reversed(big.items()))[-1] == (0, 0)

# usable in for statements
seen = []
for k in reversed({"p": 1, "q": 2}):
    seen.append(k)
assert seen == ["q", "p"]

# double reversal via materialized list is plain forward order
assert list(reversed(list(reversed(d)))) == ["a", "b", "c"]

print("test_dict_reversed_regression passed")
