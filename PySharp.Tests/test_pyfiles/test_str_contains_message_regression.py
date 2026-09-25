"""
Regression: a non-str left operand in `<x> in <str>` raises a TypeError
that names the offending type, instead of raising with an empty message.
CPython unicode_contains formats the left operand's tp_name
(Objects/unicodeobject.c), so None is reported as NoneType here — the
getargs converter's "None" spelling does not apply on this path.

CPython 3.14 reference (Objects/unicodeobject.c unicode_contains).
"""

MSG = "'in <string>' requires string as left operand, not "


def msg(fn):
    try:
        return repr(fn())
    except TypeError as e:
        return "TypeError: " + str(e)
    except Exception as e:
        return type(e).__name__ + ": " + str(e)


# --- the nine types from the report, plus neighbours ---

for value in ([1], {}, 1, (1,), None, 1.5, True, b"x", {1}, bytearray(b"x"), range(2), complex(1, 2)):
    assert msg(lambda v=value: v in "abc") == "TypeError: " + MSG + type(value).__name__, repr(value)

# an empty message was the bug, so assert the message is never blank
assert len(msg(lambda: [1] in "abc")) > len("TypeError: ")

# --- None keeps its type name on this path ---

assert msg(lambda: None in "abc") == "TypeError: " + MSG + "NoneType"
assert "None" in msg(lambda: None in "abc")

# --- a user class is named by its own class name ---

class C:
    pass

assert msg(lambda: C() in "abc") == "TypeError: " + MSG + "C"
assert msg(lambda: object() in "abc") == "TypeError: " + MSG + "object"

# --- str subclasses are accepted (PyUnicode_Check) ---

class S(str):
    pass

assert S("a") in "abc"
assert S("z") not in "abc"

# --- the same message on every route into __contains__ ---

# `not in` is the negation of the same slot
assert msg(lambda: [1] not in "abc") == "TypeError: " + MSG + "list"
# an explicit dunder call and an empty haystack reach the same check
assert msg(lambda: "abc".__contains__([1])) == "TypeError: " + MSG + "list"
assert msg(lambda: [1] in "") == "TypeError: " + MSG + "list"

# --- controls: legal containment is unchanged ---

assert "a" in "abc"
assert "" in "abc"
assert "z" not in "abc"
assert "abc" in "xabcy"
assert "abcd" not in "abc"
# empty needle against empty haystack
assert "" in ""
# non-ASCII and multi-character needles
assert "é" in "café"
assert "ab" in "abc"
assert "ac" not in "abc"

# substring search is unaffected elsewhere: `in` on other containers keeps
# its own semantics and messages — bytes has its own refusal wording
assert 1 in [1, 2]
assert "a" in {"a": 1}
assert b"a" in b"abc"
assert (1 in b"abc") is False
assert msg(lambda: [1] in b"abc") == "TypeError: a bytes-like object is required, not 'list'"
assert msg(lambda: "a" in b"abc") == "TypeError: a bytes-like object is required, not 'str'"

print("str contains message regression passed")
