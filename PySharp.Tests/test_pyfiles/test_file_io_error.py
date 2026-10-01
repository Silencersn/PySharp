"""Verifies file I/O error semantics match CPython: legal reopening of shared handles, ValueError for invalid seek whence, the single-flush r+ lifecycle, writable exclusive "x"/"x+" creation, and PermissionError for opening a directory.

:kind: test
"""

# file IO batch, aligned with CPython:
#
# - reopening the same path while another handle is open is legal
#   (CRT _SH_DENYNO semantics); share violations surface as catchable
#   PermissionError, never a raw .NET IOException
# - seek() with an invalid whence raises ValueError, not ArgumentException
# - r+ is a single read/write handle; close()/with-exit flush and dispose
#   exactly once without ObjectDisposedException
# - "x" creates exclusively AND grants write access; an existing target
#   raises FileExistsError
# - opening a directory raises PermissionError ([Errno 13] on Windows)
#   instead of FileNotFoundError

p = "t_file_io_reopen.txt"
a = open(p, "w")
a.write("x")
a.close()
b = open(p)
c = open(p)
c.close()
b.close()
print("reopen ok")

d = open(p)
e = open(p, "a")
e.write("y")
e.close()
d.close()
print("read-write-coexist ok")

# 117: invalid whence is a ValueError with the CPython message
q = "t_file_io_seek.txt"
open(q, "w").close()
f = open(q)
try:
    f.seek(0, 9)
except ValueError as ex:
    assert str(ex) == "invalid whence (9, should be 0, 1 or 2)", str(ex)
else:
    raise AssertionError("seek(0, 9) must raise ValueError")
assert f.seek(2, 0) == 2
# text mode rejects nonzero cur-relative seeks with _io.UnsupportedOperation
# (subclass of OSError and ValueError; not importable until an io module
# exists, so the type is identified by name)
for offset in (1, -1):
    try:
        f.seek(offset, 1)
    except OSError as ex:
        assert type(ex).__name__ == "UnsupportedOperation", type(ex).__name__
        assert str(ex) == "can't do nonzero cur-relative seeks", str(ex)
    else:
        raise AssertionError("nonzero cur-relative seek must raise UnsupportedOperation")
f.close()

# 231: r+ full lifecycle
r = "t_file_io_rplus.txt"
with open(r, "w") as w:
    w.write("AAAABBBB")
g = open(r, "r+")
assert g.read(4) == "AAAA"
g.seek(0)
g.write("CCCC")
g.close()
assert open(r).read() == "CCCCBBBB"
with open(r, "r+") as h:
    h.seek(0)
    h.write("DDDD")
assert open(r).read() == "DDDDBBBB"
print("rplus ok")

# 234: exclusive create is writable
xname = None
for n in range(100):
    candidate = "t_file_io_x_%d.txt" % n
    try:
        xf = open(candidate, "x")
        xname = candidate
        break
    except FileExistsError:
        continue
assert xname is not None, "no free x-name"
assert xf.writable() is True
xf.write("data")
xf.close()
assert open(xname).read() == "data"
try:
    open(xname, "x")
except FileExistsError:
    pass
else:
    raise AssertionError("x on existing file must raise FileExistsError")
xp = None
for n in range(100):
    candidate = "t_file_io_xplus_%d.txt" % n
    try:
        xp = open(candidate, "x+")
        break
    except FileExistsError:
        continue
assert xp is not None, "no free x+-name"
assert xp.readable() and xp.writable()
xp.write("abc")
xp.seek(0)
assert xp.read() == "abc"
xp.close()
print("x-mode ok")

# 235: directories are PermissionError (Windows), not FileNotFoundError
try:
    open(".")
except PermissionError as ex:
    assert "[Errno 13]" in str(ex), str(ex)
except IsADirectoryError:
    pass  # POSIX semantics
except FileNotFoundError as ex:
    raise AssertionError("open('.') must not be FileNotFoundError: " + str(ex))
print("directory ok")

# the open() failure paths construct the OSError family with the
# (errno, strerror) pair plus the filename, so the attributes and the
# [Errno N] prefix all line up
try:
    open("no_such_file_for_open_test.txt")
    raise AssertionError("FileNotFoundError expected")
except FileNotFoundError as ex:
    assert str(ex) == "[Errno 2] No such file or directory: 'no_such_file_for_open_test.txt'", str(ex)
    assert ex.args == (2, "No such file or directory"), ex.args
    assert ex.errno == 2, ex.errno
    assert ex.strerror == "No such file or directory", ex.strerror
    assert ex.filename == "no_such_file_for_open_test.txt", ex.filename

try:
    open("no_such_dir_for_open_test/f.txt")
    raise AssertionError("FileNotFoundError expected")
except FileNotFoundError as ex:
    assert ex.errno == 2 and ex.filename == "no_such_dir_for_open_test/f.txt", (ex.errno, ex.filename)

# a unique name: the suite runs several corpus processes in one working
# directory, so a generic short name would race with the other runs
exists_name = "test_413_exclusive_target.txt"
try:
    open(exists_name, "x")
except FileExistsError:
    pass
else:
    open(exists_name, "w").close()
    try:
        open(exists_name, "x")
        raise AssertionError("FileExistsError expected")
    except FileExistsError as ex:
        assert str(ex) == "[Errno 17] File exists: 'test_413_exclusive_target.txt'", str(ex)
        assert ex.args == (17, "File exists"), ex.args
        assert ex.errno == 17 and ex.filename == exists_name, (ex.errno, ex.filename)
    # no os module to unlink with: the leftover target makes the next run
    # take the except branch above, both shapes stay covered

print("ok")
