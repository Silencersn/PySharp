"""Verifies the standard streams and open() share one type: sys.stdin/stdout/stderr and a text-mode file are all _io.TextIOWrapper, with the same member surface and the same dunder protocols. The two reprs, __module__/__name__/__qualname__ and the type named in error messages all agree. Documented deviation: io is not importable yet, so isinstance(x, io.TextIOWrapper) cannot be written.

:kind: test
:cpython-diff: assert sys.stdout.errors == "strict"
:background: The standard streams used to be a separate _io.StdIo type unrelated to open()'s file object, so type(sys.stdout) is type(open(...)) was False, sys.stdout.encoding raised AttributeError, and the context-manager and iteration protocols were missing. CPython constructs sys.std* in Python/pylifecycle.c create_stdio from the same _io.TextIOWrapper type the io stack builds for open() (Modules/_io/textio.c), so the surfaces are identical.
"""

# Identity: one type object behind every text stream, whether it wraps a
# console handle or a file.
import sys

assert type(sys.stdin) is type(sys.stdout) is type(sys.stderr)

f = open("stdio_wrapper_probe.txt", "w")
assert type(f) is type(sys.stdout), (type(f), type(sys.stdout))

# The naming surfaces agree on one name, and it is the CPython one.
cls = type(sys.stdout)
assert cls.__module__ == "_io", cls.__module__
assert cls.__name__ == "TextIOWrapper", cls.__name__
assert cls.__qualname__ == "TextIOWrapper", cls.__qualname__
assert repr(cls) == "<class '_io.TextIOWrapper'>", repr(cls)

# An instance renders name, mode and encoding, like CPython's TextIOWrapper.
# The encoding is the environment's stdout codec, so only its presence and
# the field layout are asserted here; the exact spelling is host-dependent.
assert repr(sys.stdout).startswith("<_io.TextIOWrapper name='<stdout>' mode='w' encoding='"), repr(sys.stdout)
assert repr(sys.stdin).startswith("<_io.TextIOWrapper name='<stdin>' mode='r' encoding='"), repr(sys.stdin)
assert repr(sys.stderr).startswith("<_io.TextIOWrapper name='<stderr>' mode='w' encoding='"), repr(sys.stderr)

# The type name in a message is the same name the reprs show.
try:
    sys.stdout[0]
except TypeError as e:
    assert "TextIOWrapper" in str(e), str(e)
else:
    raise AssertionError("expected TypeError")

# The member surface is the union of both implementations: every name either
# side used to carry is present on both, and the dunder face too.
members = ("read", "write", "flush", "close", "readline", "readlines",
           "seek", "tell", "seekable", "readable", "writable",
           "encoding", "errors", "mode", "name", "closed",
           "__enter__", "__exit__", "__iter__", "__next__")
for name in members:
    assert hasattr(sys.stdout, name) == hasattr(f, name), (name, hasattr(sys.stdout, name), hasattr(f, name))
    assert hasattr(f, name), name

# The per-stream metadata create_stdio installs: stdin is "r", the two output
# streams "w"; stderr carries backslashreplace, the others strict.
assert sys.stdin.mode == "r" and sys.stdout.mode == "w" and sys.stderr.mode == "w"
assert sys.stdout.errors == "strict", sys.stdout.errors
assert sys.stderr.errors == "backslashreplace", sys.stderr.errors
assert sys.stdin.errors == "strict", sys.stdin.errors

# encoding is readable on every text stream, and it names a real codec: a
# round trip through it must work.
enc = sys.stdout.encoding
assert isinstance(enc, str) and enc, repr(enc)
assert "héllo".encode(enc).decode(enc) == "héllo"

# Iterating an open text file yields its lines; the same protocol is present
# on the standard streams.
f.write("one\ntwo\n")
f.close()
with open("stdio_wrapper_probe.txt") as r:
    assert list(r) == ["one\n", "two\n"], list(r)

# the encoding attribute names a real codec: a round trip through it works
enc = sys.stderr.encoding
assert isinstance(enc, str) and enc, repr(enc)
assert "héllo".encode(enc).decode(enc) == "héllo"

print("stdio wrapper identity ok")

# Closing a standard stream and then using it is the closed-file ValueError.
# stderr is closed first (its messages already landed), then the stdout
# context manager, whose __exit__ closes it — so both close checks run after
# every assertion that needs a live stream.
sys.stderr.close()
assert sys.stderr.closed is True
try:
    sys.stderr.writable()
except ValueError as e:
    assert str(e) == "I/O operation on closed file", str(e)
else:
    raise AssertionError("expected ValueError")

with sys.stdout as w:
    assert w is sys.stdout
assert sys.stdout.closed is True
try:
    sys.stdout.writable()
except ValueError as e:
    assert str(e) == "I/O operation on closed file", str(e)
else:
    raise AssertionError("expected ValueError")
