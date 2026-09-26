"""Verifies file.seek()/tell() validation follows the CPython io stack: layer-specific whence messages, UnsupportedOperation for nonzero cur/end-relative text seeks, __index__ offset handling, off_t overflow, closed-file errors, and OSError errno 22 instead of leaked .NET errors.

:kind: test
"""

# file.seek()/tell(): whence and offset validation follows the CPython
# io stack - the buffered layer rejects unknown whence values with
# "whence value N unsupported" before the closed check, the text layer
# reports "invalid whence (N, should be 0, 1 or 2)" after it; the text
# layer rejects nonzero cur/end-relative seeks with _io.UnsupportedOperation
# (a subclass of both OSError and ValueError); offsets
# go through __index__, out-of-range offsets fail the off_t conversion
# (ValueError buffered, OSError EINVAL via the OS in text mode), and a
# seek target before the start of the file is OSError errno 22 instead
# of a leaked .NET error.
open("seek_probe.txt", "w").write("hello")

def expect(exc_type, msg, fn):
    try:
        fn()
    except exc_type as e:
        assert str(e) == msg, str(e)
        return
    raise AssertionError("expected " + exc_type.__name__)

# UnsupportedOperation is not importable until PySharp grows an io
# module, so the type is identified by name through its OSError base
def expect_unsupported(msg, fn):
    try:
        fn()
    except OSError as e:
        assert type(e).__name__ == "UnsupportedOperation", type(e).__name__
        assert str(e) == msg, str(e)
        return
    raise AssertionError("expected UnsupportedOperation")

# normal operation, both layers
t = open("seek_probe.txt")
assert t.seek(3) == 3
assert t.tell() == 3
assert t.seek(0, 2) == 5
assert t.seek(0, 1) == 5
t.close()

# text mode rejects nonzero cur/end-relative seeks (zero offsets stay
# supported: seek(0, 1) resyncs, seek(0, 2) jumps to EOF)
expect_unsupported("can't do nonzero cur-relative seeks",
                   lambda: open("seek_probe.txt").seek(1, 1))
expect_unsupported("can't do nonzero cur-relative seeks",
                   lambda: open("seek_probe.txt").seek(-2, 1))
expect_unsupported("can't do nonzero end-relative seeks",
                   lambda: open("seek_probe.txt").seek(1, 2))

b = open("seek_probe.txt", "rb")
assert b.seek(3) == 3
assert b.tell() == 3
assert b.seek(2, 1) == 5
assert b.seek(-1, 2) == 4
assert b.seek(-4, 2) == 1
b.seek(0)
b.close()

# invalid whence: message differs per layer
expect(ValueError, "invalid whence (9, should be 0, 1 or 2)",
       lambda: open("seek_probe.txt").seek(0, 9))
expect(ValueError, "whence value 9 unsupported",
       lambda: open("seek_probe.txt", "rb").seek(0, 9))
expect(ValueError, "whence value -1 unsupported",
       lambda: open("seek_probe.txt", "rb").seek(0, -1))

# negative seek positions
expect(ValueError, "negative seek position -1",
       lambda: open("seek_probe.txt").seek(-1))
expect(OSError, "[Errno 22] Invalid argument",
       lambda: open("seek_probe.txt", "rb").seek(-1))
expect(OSError, "[Errno 22] Invalid argument",
       lambda: open("seek_probe.txt", "rb").seek(-100, 2))

# offsets must support __index__ in both layers
expect(TypeError, "'str' object cannot be interpreted as an integer",
       lambda: open("seek_probe.txt", "rb").seek('0'))
expect(TypeError, "'str' object cannot be interpreted as an integer",
       lambda: open("seek_probe.txt", "rb").seek(0, 'x'))
expect(TypeError, "'float' object cannot be interpreted as an integer",
       lambda: open("seek_probe.txt", "rb").seek(1.5))

class Idx:
    def __index__(self):
        return 3

class EndIdx:
    def __index__(self):
        return 2

f = open("seek_probe.txt")
assert f.seek(Idx()) == 3
f.close()
g = open("seek_probe.txt", "rb")
assert g.seek(-1, EndIdx()) == 4
g.close()

# off_t conversion failures
expect(ValueError, "cannot fit 'int' into an offset-sized integer",
       lambda: open("seek_probe.txt", "rb").seek(2 ** 63))
expect(OSError, "[Errno 22] Invalid argument",
       lambda: open("seek_probe.txt").seek(2 ** 63))

# closed files: seek and tell messages differ per layer
cf = open("seek_probe.txt")
cf.close()
expect(ValueError, "I/O operation on closed file.", lambda: cf.seek(0))
expect(ValueError, "I/O operation on closed file.", lambda: cf.tell())

cb = open("seek_probe.txt", "rb")
cb.close()
expect(ValueError, "seek of closed file", lambda: cb.seek(0))
expect(ValueError, "I/O operation on closed file", lambda: cb.tell())

# validation order: the buffered layer rejects a bad whence before the
# closed check, the text layer reports the closed file first
expect(ValueError, "whence value 9 unsupported", lambda: cb.seek(0, 9))
expect(ValueError, "I/O operation on closed file.", lambda: cf.seek(0, 9))

# the original crash scenario: invalid whence must be a catchable
# Python exception, never a raw .NET one
f = open("seek_probe.txt")
try:
    f.seek(0, 9)
except ValueError:
    pass
else:
    raise AssertionError("expected ValueError")
f.close()
