"""
Regression: open().readline() at EOF returns ''/b'' (not StopIteration),
while iteration (__next__) raises StopIteration once exhausted.
"""
import sys

# Text mode: readline() at EOF returns ''.
with open("_test_readline_eof.txt", "w") as f:
    f.write("line1\nline2\n")

with open("_test_readline_eof.txt", "r") as f:
    line1 = f.readline()
    line2 = f.readline()
    line3 = f.readline()
    assert line1 == "line1\n", f"line1 = {line1!r}"
    assert line2 == "line2\n", f"line2 = {line2!r}"
    assert line3 == "", f"readline() at EOF = {line3!r}"

# Iteration collects all lines and stops cleanly.
with open("_test_readline_eof.txt", "r") as f:
    lines = list(f)
    assert lines == ["line1\n", "line2\n"], f"lines = {lines}"

# next() raises StopIteration once exhausted.
with open("_test_readline_eof.txt", "r") as f:
    assert f.readline() == "line1\n"
    assert f.readline() == "line2\n"
    assert f.readline() == ""
    it = iter(f)
    try:
        next(it)
        assert False, "iteration should raise StopIteration at EOF"
    except StopIteration:
        pass

# Binary mode: readline() at EOF returns b''.
with open("_test_readline_eof.bin", "wb") as f:
    f.write(b"a\nb\n")

with open("_test_readline_eof.bin", "rb") as f:
    line1 = f.readline()
    line2 = f.readline()
    line3 = f.readline()
    assert line1 == b"a\n", f"bin line1 = {line1!r}"
    assert line2 == b"b\n", f"bin line2 = {line2!r}"
    assert line3 == b"", f"binary readline() at EOF = {line3!r}"
    it = iter(f)
    try:
        next(it)
        assert False, "binary iteration should raise StopIteration at EOF"
    except StopIteration:
        pass

print("test_readline_eof_regression passed")
