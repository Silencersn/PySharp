"""
str concatenation keeps adjacent lone surrogates as separate code points.

CPython's str is a code-point sequence: a pair built from two lone surrogates
(U+D800 followed by U+DC00, written '\\ud800\\udc00' in source escapes) has
len 2, and no operation may merge it into U+10000 — an astral character that
happens to share the UTF-16 units. PySharp stores UTF-16, so the producers
that know the code-point sequence (concat, join, %-formatting, f-strings,
literals, repeat) carry it alongside the payload whenever a lone high
surrogate ends up next to a lone low one.

CPython 3.14 reference:
    len("\\ud800" + "\\udc00")  -> 2, repr is the two escaped code points
    (hi + lo).encode("utf-8") -> UnicodeEncodeError: surrogates not allowed
    "\\ud800" + "\\udc00" != "\\U00010000"
    baseline astral characters ("\\U0001F600") still count as one code point

The escapes are double-backslashed in this docstring: a docstring is an
ordinary string constant, and single ones would embed real lone surrogates
that CPython refuses to process.

:kind: test
"""

hi, lo = "\ud800", "\udc00"
paired = hi + lo

# the issue's matrix: every concatenation path
assert len(paired) == 2 and repr(paired) == "'\\ud800\\udc00'"
assert len("".join([hi, lo])) == 2 and "".join([hi, lo]) == paired
assert len("%s%s" % (hi, lo)) == 2 and "%s%s" % (hi, lo) == paired
assert len(f"{hi}{lo}") == 2 and f"{hi}{lo}" == paired
assert len("\ud800\udc00") == 2 and "\ud800\udc00" == paired
assert len("\ud800" "\udc00") == 2 and "\ud800" "\udc00" == paired  # implicit concat

# a lone surrogate must not survive UTF-8 as an astral character
try:
    paired.encode("utf-8")
    assert False, "encode should have raised"
except UnicodeEncodeError as e:
    assert str(e) == ("'utf-8' codec can't encode characters in position 0-1: "
                      "surrogates not allowed"), str(e)
assert paired.encode("utf-8", "surrogatepass") == b"\xed\xa0\x80\xed\xb0\x80"
assert paired.encode("utf-8", "ignore") == b""
assert paired.encode("utf-8", "backslashreplace") == b"\\ud800\\udc00"

# equality, ordering and hashing follow the code-point sequence
assert paired != "\U00010000" and "\U00010000" != paired
assert paired < "\U00010000" and not ("\U00010000" < paired)
assert paired == ("\ud800" + "\udc00") and hash(paired) == hash("\ud800" + "\udc00")
d = {paired: 1, "\U00010000": 2}
assert len(d) == 2 and d[paired] == 1 and d["\U00010000"] == 2
assert paired in d and (hi + lo) in {paired}
assert "\U00010000" not in {paired}

# iteration, indexing, slicing and repetition see two code points
assert [c for c in paired] == ["\ud800", "\udc00"]
assert list(paired) == ["\ud800", "\udc00"]
assert paired[0] == "\ud800" and paired[1] == "\udc00" and ord(paired[0]) == 0xD800
assert paired[0:1] == "\ud800" and paired[1:] == "\udc00" and paired[::-1] == "\udc00\ud800"
assert len(paired * 2) == 4 and paired * 2 == "\ud800\udc00\ud800\udc00"

# membership over code points
assert "\ud800" in paired and "\U00010000" not in paired
assert "\ud800\udc00" in paired

# %-formatting keeps the sequence through padding and %c
assert len("%-4s|" % paired) == 5 and "%-4s|" % paired == paired + "  |"
assert len("%r" % paired) == 14 and "%r" % paired == "'\\ud800\\udc00'"
assert "%c" % 0xD800 == "\ud800"

# the issue's regression baseline: untouched behaviors
assert len(hi * 2) == 2 and hi * 2 == "\ud800\ud800"
assert len("\U0001F600") == 1 and repr("\U0001F600") == "'\U0001F600'"
assert len("\U0001F600" + "\U0001F600") == 2
assert len("\U0001F600" + hi) == 2 and "\U0001F600" + hi == "\U0001F600\ud800"
assert len(hi + "X" + lo) == 3 and hi + "X" + lo == "\ud800X\udc00"
assert len("\U00010000") == 1 and "\U00010000" == "\U00010000"
assert len("😀") == 1 and "😀".upper() == "😀"
assert len("\U0001F600".upper()) == 1

print("test_str_lone_surrogate_concat passed")
