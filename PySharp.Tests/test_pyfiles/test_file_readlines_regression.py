"""
Regression: file objects must expose readlines() with CPython's
_IOBase.readlines hint semantics — read lines until the accumulated size
EXCEEDS a positive hint (the crossing line is kept); hint <= 0 means no
limit; text mode counts characters, binary mode counts bytes. A closed file
raises ValueError; unknown attributes raise AttributeError.

CPython 3.14 reference (file content 'a\nbb\nccc\ndddd\n'):
    f.readlines()      -> ['a\n', 'bb\n', 'ccc\n', 'dddd\n']
    f.readlines(4)     -> ['a\n', 'bb\n']        (2 <= 4 < 5, stops at 5)
    f.readlines(5)     -> ['a\n', 'bb\n', 'ccc\n'] (5 is not exceeded)
    f.readlines(1)     -> ['a\n']
    f.readlines(0/-1)  -> all lines
    closed readlines   -> ValueError: I/O operation on closed file.
"""

with open("readlines_probe.bin", "wb") as _f:
    _f.write(b"a\nbb\nccc\ndddd\n")


def check(label, fn, expected):
    try:
        actual = fn()
    except BaseException as e:
        actual = (type(e).__name__, str(e))
    assert actual == expected, f"{label}: {actual!r} != {expected!r}"


f = open("readlines_probe.bin")
assert hasattr(f, "readlines") is True
check("all", f.readlines, ["a\n", "bb\n", "ccc\n", "dddd\n"])
f.close()

f = open("readlines_probe.bin")
check("hint4", lambda: f.readlines(4), ["a\n", "bb\n"])
f.close()

f = open("readlines_probe.bin")
check("hint5", lambda: f.readlines(5), ["a\n", "bb\n", "ccc\n"])
f.close()

f = open("readlines_probe.bin")
check("hint2", lambda: f.readlines(2), ["a\n", "bb\n"])
f.close()

f = open("readlines_probe.bin")
check("hint1", lambda: f.readlines(1), ["a\n"])
f.close()

f = open("readlines_probe.bin")
check("hint0", lambda: f.readlines(0), ["a\n", "bb\n", "ccc\n", "dddd\n"])
f.close()

f = open("readlines_probe.bin")
check("hint-1", lambda: f.readlines(-1), ["a\n", "bb\n", "ccc\n", "dddd\n"])
f.close()

f = open("readlines_probe.bin")
check("mixed", lambda: (f.readlines(3), f.readline()),
      (["a\n", "bb\n"], "ccc\n"))
f.close()

f = open("readlines_probe.bin", "rb")
check("binary", f.readlines, [b"a\n", b"bb\n", b"ccc\n", b"dddd\n"])
f.close()

f = open("readlines_probe.bin", "rb")
check("binary hint5", lambda: f.readlines(5), [b"a\n", b"bb\n", b"ccc\n"])
f.close()

f = open("readlines_probe.bin")
f.close()
try:
    f.readlines()
except ValueError as e:
    assert str(e) == "I/O operation on closed file."
else:
    raise AssertionError("readlines on a closed file must raise ValueError")

f = open("readlines_probe.bin")
try:
    f.zzz_property
except AttributeError:
    pass
else:
    raise AssertionError("unknown file attribute must raise AttributeError")
f.close()

print("test_file_readlines_regression passed")
