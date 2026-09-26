"""str.splitlines keepends is a plain truth test on the raw argument: any truthy object keeps the line endings, falsy ones drop them.

:kind: test
"""

# Regression: str.splitlines' keepends argument is a plain truth test
# on the raw argument (CPython PyObject_IsTrue) — int 1/2, floats and
# any other truthy object keep the line endings; None, 0, "" and
# empty containers do not. Previously int truthy values were silently
# treated as False because only bool was accepted.
class Idx:
    def __index__(self):
        return 2

class ZeroLen:
    def __len__(self):
        return 0

class Truthy:
    def __bool__(self):
        return True

class Bad:
    def __bool__(self):
        raise RuntimeError("boom")

KEEP = ["a\n", "b"]
DROP = ["a", "b"]

assert "a\nb".splitlines(True) == KEEP
assert "a\nb".splitlines(1) == KEEP
assert "a\nb".splitlines(2) == KEEP
assert "a\nb".splitlines(0) == DROP
assert "a\nb".splitlines(False) == DROP
assert "a\nb".splitlines(None) == DROP
assert "a\nb".splitlines(1.5) == KEEP
assert "a\nb".splitlines(0.0) == DROP
assert "a\nb".splitlines("x") == KEEP
assert "a\nb".splitlines("") == DROP
assert "a\nb".splitlines([]) == DROP
assert "a\nb".splitlines((1,)) == KEEP
assert "a\nb".splitlines(object()) == KEEP
assert "a\nb".splitlines(Idx()) == KEEP
assert "a\nb".splitlines(10**100) == KEEP
assert "a\nb".splitlines(ZeroLen()) == DROP
assert "a\nb".splitlines(Truthy()) == KEEP
assert "a\nb".splitlines() == DROP
assert "a\r\nb".splitlines(1) == ["a\r\n", "b"]

try:
    "a\nb".splitlines(Bad())
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "boom"

print("test_splitlines_keepends_truth passed")
