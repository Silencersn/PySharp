"""
str.startswith/endswith must accept a tuple of prefixes/
suffixes (any match wins, evaluated left to right), reject non-str items
inside the tuple and non-str non-tuple first args with CPython's exact
messages, and keep the str-path window semantics untouched.

:kind: test
"""

# tuple accept/reject, any match wins
assert "hello".startswith(("x", "he")) is True
assert "hello".endswith(("lo", "x")) is True
assert "hello".startswith(("x", "y")) is False
assert "hello".endswith(("x", "y")) is False

# an empty tuple has no candidate
assert "hello".startswith(()) is False
assert "hello".endswith(()) is False

# tuples combine with start/end windows
assert "hello".startswith(("e", "l"), 1) is True
assert "hello".startswith(("h", "e"), 1, 3) is True
assert "hello".endswith(("ell", "o"), 0, 4) is True
assert "hello".startswith(("",), 5) is True
assert "hello".startswith(("",), 6) is False

# an empty needle in the tuple matches at any valid position
assert "abc".startswith(("", "z")) is True

# non-str items inside the tuple get CPython's exact messages
try:
    "hello".startswith(("x", 42))
    raise AssertionError("int tuple item accepted")
except TypeError as e:
    assert str(e) == "tuple for startswith must only contain str, not int", str(e)

try:
    "hello".endswith((b"x",))
    raise AssertionError("bytes tuple item accepted")
except TypeError as e:
    assert str(e) == "tuple for endswith must only contain str, not bytes", str(e)

# non-str non-tuple first args get CPython's exact messages
try:
    "hello".startswith(42)
    raise AssertionError("int first arg accepted")
except TypeError as e:
    assert str(e) == "startswith first arg must be str or a tuple of str, not int", str(e)

try:
    "hello".endswith(b"lo")
    raise AssertionError("bytes first arg accepted")
except TypeError as e:
    assert str(e) == "endswith first arg must be str or a tuple of str, not bytes", str(e)

# a list is not a tuple
try:
    "hello".startswith(["x", "he"])
    raise AssertionError("list accepted")
except TypeError as e:
    assert str(e) == "startswith first arg must be str or a tuple of str, not list", str(e)

# tuple subclasses behave as tuples
class T(tuple):
    pass


assert "hello".startswith(T(("x", "he"))) is True

# the str path keeps its window semantics, negatives included
assert "hello".startswith("he", -4) is False
assert "hello".endswith("lo", 0, -1) is False
assert "hello".startswith("ell", 1, 4) is True
assert "hello".startswith("he", 10) is False
assert "hello".startswith("he", None, None) is True

print("test_str_tailmatch_tuple passed")
