"""
Regression: str.splitlines accepts keepends as a keyword argument. CPython's
clinic signature is splitlines(self, /, keepends=False) — the slash sits
after self, so only self is positional-only and keepends binds by name as
well as by position. PySharp had marked it positional-only, so the standard
splitlines(keepends=True) spelling raised TypeError.

Assertions on error *wording* are deliberately loose (type plus the
distinguishing token): the surrounding arity/keyword message families still
diverge from CPython and are tracked separately, so pinning their text here
would lock in a divergence rather than the fix.

CPython 3.14 reference (Objects/clinic/unicodeobject.c.h str.splitlines).
"""


def err(fn):
    """The exception type and message of a call expected to fail."""
    try:
        fn()
    except Exception as e:
        return type(e).__name__, str(e)
    raise AssertionError("expected an exception")


TEXT = "a\nb\n"

# --- the fixed case: keepends binds by keyword ---

assert "ab".splitlines(keepends=True) == ["ab"]
assert "ab".splitlines(keepends=False) == ["ab"]
assert TEXT.splitlines(keepends=True) == ["a\n", "b\n"]
assert TEXT.splitlines(keepends=False) == ["a", "b"]

# keyword and position agree, including the truthy/falsy spread
assert TEXT.splitlines(keepends=1) == ["a\n", "b\n"]
assert TEXT.splitlines(keepends=0) == ["a", "b"]
assert TEXT.splitlines(True) == ["a\n", "b\n"]
assert TEXT.splitlines(False) == ["a", "b"]
assert TEXT.splitlines(keepends=1) == TEXT.splitlines(True)
# no argument at all keeps the default (no line endings)
assert TEXT.splitlines() == ["a", "b"]
assert "ab".splitlines() == ["ab"]

# **-unpacking reaches the same binding
assert TEXT.splitlines(**{"keepends": True}) == ["a\n", "b\n"]

# --- universal newlines: all of them split, keepends keeps them ---

assert "a\r\nb\rc\v".splitlines(keepends=True) == ["a\r\n", "b\r", "c\v"]
assert "a\r\nb\rc\v".splitlines(keepends=False) == ["a", "b", "c"]
assert "a\nb".splitlines(keepends=True) == ["a\n", "b"]
assert "".splitlines(keepends=True) == []
assert "\n".splitlines(keepends=True) == ["\n"]

# --- keepends is a plain truth test, not a type check ---

assert TEXT.splitlines(keepends=2) == ["a\n", "b\n"]
assert TEXT.splitlines(keepends="x") == ["a\n", "b\n"]
assert TEXT.splitlines(keepends=[]) == ["a", "b"]

# --- error faces: still refused, with the offending name named ---

# an unknown keyword, and CPython's "did you mean" suggestion
kind, text = err(lambda: "ab".splitlines(keepend=True))
assert kind == "TypeError", (kind, text)
assert "unexpected keyword argument 'keepend'" in text, text
assert "Did you mean 'keepends'?" in text, text

kind, text = err(lambda: "ab".splitlines(x=1))
assert kind == "TypeError", (kind, text)
assert "unexpected keyword argument 'x'" in text, text

# too many arguments, however they are passed; CPython's count-based
# message names no argument, so only the type is asserted
kind, _ = err(lambda: "ab".splitlines(True, False))
assert kind == "TypeError", kind
kind, _ = err(lambda: "ab".splitlines(True, keepends=False))
assert kind == "TypeError", kind

# --- controls: neighbouring str methods keep their own binding modes ---

# split/rsplit/encode/replace-by-count take keywords on both sides
assert "a,b".split(sep=",") == ["a", "b"]
assert "a,b".rsplit(sep=",", maxsplit=1) == ["a", "b"]
assert "abc".encode(encoding="utf-8") == b"abc"
assert "abc".replace("a", "x", count=1) == "xbc"

# genuinely positional-only parameters stay positional-only. Both sides
# refuse these calls, but with different wording families (CPython's
# "takes no keyword arguments" vs PySharp's positional-only list), so only
# the refusal and the receiving method are asserted
for call, method in (
    (lambda: "a".center(5, fillchar="*"), "center"),
    (lambda: "abc".replace(old="a", new="x"), "replace"),
    (lambda: "abc".partition(sep="b"), "partition"),
    (lambda: "  a  ".strip(chars=" "), "strip"),
    (lambda: "a".zfill(width=5), "zfill"),
    (lambda: "abc".removeprefix(prefix="a"), "removeprefix"),
):
    kind, text = err(call)
    assert kind == "TypeError", (method, kind, text)
    assert method in text, (method, text)

print("splitlines keepends keyword regression passed")
