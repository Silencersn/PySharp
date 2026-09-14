# Regression: str search-method start/end bounds go through CPython's
# _PyEval_SliceIndex — None keeps the method default, anything with
# __index__ converts (saturating for huge magnitudes), and everything
# else raises "slice indices must be integers or None or have an
# __index__ method" instead of being silently ignored.
class Idx:
    def __index__(self):
        return 2

class Bad:
    def __index__(self):
        raise RuntimeError("boom")

s = "abcabc"
MSG = "slice indices must be integers or None or have an __index__ method"

# __index__ protocol is honored
assert s.count("a", Idx(), 5) == 1
assert s.find("a", Idx(), 5) == 3
assert s.rfind("a", Idx(), 5) == 3
assert s.index("b", Idx()) == 4
assert s.count("b", Idx(), 5) == 1
assert s.count("a", 0, Idx()) == 1
assert s.rfind("a", 1, Idx()) == -1
assert s.startswith("b", Idx()) is False
assert s.startswith(("ca", "x"), Idx(), 5) is True

# non-indexable objects raise the slice-indices TypeError
for desc, fn in [
    ("count float", lambda: s.count("a", 1.0, 5)),
    ("count str", lambda: s.count("a", "1", 5)),
    ("find float", lambda: s.find("a", 1.0)),
    ("rfind float", lambda: s.rfind("c", 1.5)),
    ("index float", lambda: s.index("a", 1.0)),
    ("rindex float", lambda: s.rindex("c", 1.5)),
    ("count float solo", lambda: s.count("a", 1.5)),
    ("startswith float", lambda: s.startswith("a", 1.5)),
    ("startswith str", lambda: s.startswith("a", "1", 5)),
    ("endswith float", lambda: s.endswith("a", 1.5)),
    ("tuple prefix float", lambda: s.startswith(("a",), 1.5)),
    ("find end float", lambda: s.find("a", 0, 1.5)),
]:
    try:
        fn()
        raise AssertionError("expected TypeError: " + desc)
    except TypeError as e:
        assert str(e) == MSG, (desc, str(e))

# None keeps the defaults; bool converts through __index__
assert s.count("a", None, None) == 2
assert s.count("a", True, 4) == 1

# huge magnitudes keep the saturating behavior
assert s.find("a", -10**10) == 0
assert s.count("a", 10**100) == 0

# defaults and user exceptions propagate untouched
assert (s.find("a"), s.count("a"), s.startswith("abc")) == (0, 2, True)
try:
    s.find("a", Bad())
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "boom"

print("test_str_search_slice_index_regression passed")
