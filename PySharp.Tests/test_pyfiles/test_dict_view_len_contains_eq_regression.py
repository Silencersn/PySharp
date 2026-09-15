# Regression: dict views expose the CPython view protocol — sq_length
# reads the live source size (dictview_len), keys/items membership goes
# through the source dict (dictkeys_contains / dictitems_contains), and
# the set-like richcompare (dictview_richcompare) gates on lengths
# before an all_contained_in scan. dict_values has no sq_contains and
# no tp_richcompare, so it stays iteration-only and identity-compared.

d = {"a": 1}
ks = d.keys()
d["b"] = 2

# the len protocol is live: it reflects insertions
assert len(ks) == 2
assert ks.__len__() == 2

# membership stays a direct source lookup after mutation
assert "b" in ks
assert "c" not in ks

vs = d.values()
its = d.items()
assert len(vs) == 2 and len(its) == 2

# dict_items contains follows dictitems_contains: only a 2-tuple can match
assert ("a", 1) in its
assert ("a", 9) not in its
assert ["a", 1] not in its
assert ("a", 1, 2) not in its
assert "ab" not in its

# values views have no sq_contains; `in` scans the iterator
assert 1 in vs and 9 not in vs
assert not hasattr(vs, "__contains__")

# set-like richcompare compares contents, not identity
assert ks == {"a", "b"}
assert ks == frozenset({"b", "a"})
assert ks != {"a"}
assert ks != "x"
assert (ks == {"a": 1, "b": 2}) is False
assert ks == ks
assert ks == d.copy().keys()
assert d.copy().keys() == ks

# ordering comparisons gate on length, then scan containment
assert ks <= {"a", "b"} and not ks < {"a", "b"}
assert ks < {"a", "b", "c"}
assert ks >= {"a"} and ks > {"a"}
assert not ks >= {"a", "b", "c"}
assert {"a", "b"} >= ks and not {"a", "b"} > ks

# items views are set-like too
assert its == {("a", 1), ("b", 2)}
assert its <= {("a", 1), ("b", 2)}

try:
    ks < 1
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "'<' not supported between instances of 'dict_keys' and 'int'", e

# views update live through deletion as well
del d["b"]
assert len(ks) == 1 and len(vs) == 1 and len(its) == 1
assert ks == {"a"} and ks <= {"a", "b"} and not ks == {"a", "b"}

d.clear()
assert len(ks) == 0 and not ks and ks == set() and ks == frozenset()
assert bool({}.keys()) is False and bool(d.items()) is False
assert bool({"x": 0}.values()) is True

# dict_values has no richcompare: two values views are not equal,
# and a values view never equals a list
dv = {"x": 10}.values()
assert dv == dv
assert dv != {"x": 10}.values()
assert (dv == [10]) is False
assert (dv == ["x"]) is False

print("test_dict_view_len_contains_eq_regression passed")
