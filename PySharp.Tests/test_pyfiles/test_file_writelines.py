"""Verifies IOBase.writelines: closed-before-iterable ordering, separator-free writes through the object's write(), generator input, TypeError shapes, and the one-shot "open(p, mode).writelines(...)" idiom landing on disk without an explicit close in both binary and text mode (issue 416).

:kind: test
"""

# test_file_writelines: Verify writelines across text/binary modes


def read_bytes(path):
    with open(path, "rb") as f:
        return f.read()


def expect(exc_type, func, *args):
    try:
        func(*args)
    except exc_type as e:
        return str(e)
    raise AssertionError("expected " + exc_type.__name__)


# Test 1: one-shot binary writelines without close — every write() to an
# open() file flushes, so the bytes are visible to a same-process reader
# (the issue 416 idiom)
print("=== Test 1: one-shot binary ===")
open("_wl1.bin", "wb").writelines([b"pay", b"load"])
assert read_bytes("_wl1.bin") == b"payload"
print("OK: one-shot binary")

# Test 2: no separators are added, and the return value is None
print("=== Test 2: no separators, returns None ===")
f = open("_wl1.bin", "wb")
assert f.writelines([b"a", b"bb", b"ccc"]) is None
f.close()
assert read_bytes("_wl1.bin") == b"abbccc"
print("OK: no separators")

# Test 3: text mode joins without separators too
print("=== Test 3: text mode ===")
open("_wl1.txt", "w").writelines(["x", "y", "z"])
with open("_wl1.txt", "r") as r:
    assert r.read() == "xyz"
print("OK: text mode")

# Test 4: any iterable works, including a generator; an empty iterable
# writes nothing
print("=== Test 4: iterables ===")
f = open("_wl2.bin", "wb")
f.writelines(bytes([c]) for c in b"chain")
f.close()
assert read_bytes("_wl2.bin") == b"chain"
f = open("_wl2.bin", "ab")
assert f.writelines([]) is None
f.close()
assert read_bytes("_wl2.bin") == b"chain"
print("OK: iterables")

# Test 5: the closed check runs before the iterable is even inspected
print("=== Test 5: closed check ===")
g = open("_wl2.bin", "wb")
g.close()
assert expect(ValueError, g.writelines, [b"x"]) == "I/O operation on closed file."
assert expect(ValueError, g.writelines, 1) == "I/O operation on closed file."
print("OK: closed check")

# Test 6: a non-iterable argument raises the standard TypeError; element
# type errors surface from write() with write()'s own messages
print("=== Test 6: type errors ===")
f = open("_wl2.bin", "wb")
assert expect(TypeError, f.writelines, 123) == "'int' object is not iterable"
tf = open("_wl3.txt", "w")
assert expect(TypeError, tf.writelines, ["ok", 42]) == "write() argument must be str, not int"
tf.close()
bf = open("_wl3.bin", "wb")
assert expect(TypeError, bf.writelines, [b"ok", "bad"]) == "a bytes-like object is required, not 'str'"
bf.close()
print("OK: type errors")

# Test 7: an error partway through iteration leaves the earlier writes in
# the file
print("=== Test 7: partial writes ===")
def failing_gen():
    yield b"a"
    yield b"b"
    raise RuntimeError("boom")

f = open("_wl4.bin", "wb")
try:
    f.writelines(failing_gen())
except RuntimeError:
    pass
f.close()
assert read_bytes("_wl4.bin") == b"ab"
print("OK: partial writes")

# Test 8: many small lines all land without an explicit close — the
# one-shot export shape at a size past any write buffering
print("=== Test 8: many small lines ===")
lines = []
for i in range(2000):
    lines.append(str(i).encode() + b"\n")
open("_wl5.bin", "wb").writelines(lines)
expected_len = sum(len(s) for s in lines)
assert expected_len > 4096
assert len(read_bytes("_wl5.bin")) == expected_len
print("OK: many small lines")

print()
print("All writelines tests passed")
