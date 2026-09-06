"""
Regression: an empty prefix/suffix in str.startswith / str.endswith must
match at every valid window position, like CPython tailmatch (the clamped
[start, end) window only has to fit the needle). PySharp used to return
False for every zero-width window (an early "start >= end" check), so
"".endswith("") and "abc".startswith("", 1, 1) were False.

A positive start beyond the length is not clamped (CPython adjust_indices),
so the window check must keep an empty needle False past the end - those
boundaries are pinned as guards. Window and needle lengths are counted in
runes, so non-BMP needles are covered too.
"""

DA = "\U0001F600"  # non-BMP rune

AB = "a" + DA + "b"


# red cases: empty needle at valid zero-width windows
assert "".endswith("") is True
assert "".startswith("") is True
assert "abc".startswith("", 0, 0) is True
assert "abc".startswith("", 1, 1) is True
assert "abc".startswith("", 3, 3) is True     # start == length
assert "abc".endswith("", 0, 0) is True
assert "abc".endswith("", 1, 1) is True
assert "abc".endswith("", 3, 3) is True
assert "abc".startswith("", -5, -4) is True   # negatives clamp to zero width
assert "abc".endswith("", -5, -4) is True
assert AB.startswith("", 1, 1) is True
assert AB.endswith("", 1, 1) is True


# guards: already-correct boundaries must stay correct
assert "abc".startswith("") is True
assert "abc".endswith("") is True
assert "abc".startswith("", 4) is False       # start beyond the length
assert "abc".startswith("", 5, 6) is False
assert "abc".endswith("", 4) is False
assert "abc".endswith("", 5, 6) is False
assert "".startswith("", 1) is False          # start beyond an empty string
assert "".endswith("", 1) is False
assert "abc".startswith("", 2, 1) is False    # end below start
assert "abc".endswith("", 2, 1) is False
assert "abc".startswith("abc", 0, 2) is False  # needle longer than the window
assert "abc".endswith("abc", 0, 2) is False
assert "abc".startswith("ab", 0, 2) is True
assert "abc".endswith("c", 2, 3) is True
assert "abc".startswith("", -1) is True
assert "abc".endswith("", -1) is True
assert AB.startswith(DA, 1, 2) is True        # rune-unit window and needle
assert AB.endswith(DA, 1, 2) is True

print("test_empty_needle_tailmatch_regression passed")
