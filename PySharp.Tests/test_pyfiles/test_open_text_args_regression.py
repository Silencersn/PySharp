# Regression (issue #236): open() must accept the buffering=, encoding=,
# errors= and newline= parameters of the CPython signature. Text-only
# arguments are rejected in binary mode before the file opens, the
# buffering checks and the codec lookup fail after it (the handle is
# released again), unknown error handler names surface lazily at the first
# error, and f.encoding / f.errors carry the values as passed.
open("_t236enc.txt", "w", encoding="utf-8", newline="").write("héllo")
f = open("_t236enc.txt", encoding="utf-8", newline="")
assert f.read() == "héllo"
assert f.encoding == "utf-8", f.encoding
assert f.errors == "strict", f.errors
f.close()
f = open("_t236enc.txt", encoding="UTF-8")
assert f.encoding == "UTF-8", f.encoding
f.close()

# errors= handlers
open("_t236bad.txt", "wb").write(b"a\xffzb")
try:
    open("_t236bad.txt", encoding="utf-8").read()
except UnicodeDecodeError as e:
    assert e.encoding == "utf-8", e.encoding
    assert e.reason == "invalid start byte", e.reason
else:
    raise AssertionError("expected UnicodeDecodeError")
assert open("_t236bad.txt", encoding="utf-8", errors="ignore").read() == "azb"
assert open("_t236bad.txt", encoding="utf-8", errors="replace").read() == "a\ufffdzb"
assert open("_t236bad.txt", encoding="utf-8", errors="backslashreplace").read() == "a\\xffzb"

# CPython resolves the handler name lazily: a valid file never rejects it
f = open("_t236enc.txt", encoding="utf-8", errors="totally-bogus")
assert f.read() == "héllo"
f.close()
# and an invalid name fails at the first error
f = open("_t236bad.txt", encoding="utf-8", errors="totally-bogus")
try:
    f.read()
except LookupError as e:
    assert "unknown error handler" in str(e), str(e)
else:
    raise AssertionError("expected LookupError")
f.close()

# newline= read modes (content has \r\n and \n terminators)
open("_t236nl.txt", "w", newline="").write("x\r\ny\nz")
assert open("_t236nl.txt", newline="").readlines() == ["x\r\n", "y\n", "z"]
assert open("_t236nl.txt").readlines() == ["x\n", "y\n", "z"]
f = open("_t236nl.txt", newline="\r")
assert f.readline() == "x\r", repr(f.readline())
assert f.readline() == "\ny\nz", repr(f.readline())
assert f.readline() == ""
f.close()
f = open("_t236nl.txt", newline="\n")
assert f.readline() == "x\r\n", repr(f.readline())
assert f.readline() == "y\n"
assert f.readline() == "z"
f.close()

# newline= write translation: None expands to os.linesep, '' and '\n' keep
# the text, a fixed value maps '\n' onto it
open("_t236wnl.txt", "w", newline="").write("a\nb")
assert open("_t236wnl.txt", "rb").read() == b"a\nb"
open("_t236wnl2.txt", "w", newline="\r\n").write("a\nb")
assert open("_t236wnl2.txt", "rb").read() == b"a\r\nb"
open("_t236wnl3.txt", "w").write("a\nb")
assert open("_t236wnl3.txt", "rb").read() == b"a\r\nb"

# buffering=: binary line buffering warns but opens; text rejects 0 after
# open; a non-int goes through __index__
fb = open("_t236wnl.txt", "rb", buffering=1)
fb.close()
try:
    open("_t236wnl.txt", "r", buffering=0)
except ValueError as e:
    assert str(e) == "can't have unbuffered text I/O", str(e)
else:
    raise AssertionError("expected ValueError")
try:
    open("_t236wnl.txt", "rb", buffering="4")
except TypeError as e:
    assert str(e) == "'str' object cannot be interpreted as an integer", str(e)
else:
    raise AssertionError("expected TypeError")

# binary mode takes none of the text arguments, checked before the file opens
for kwargs in ({"encoding": "utf-8"}, {"errors": "ignore"}, {"newline": ""}):
    try:
        open("_nonexistent_236.txt", "rb", **kwargs)
    except ValueError as e:
        assert "binary mode doesn't take" in str(e), str(e)
    else:
        raise AssertionError("expected ValueError for " + str(kwargs))
try:
    open("_nonexistent_236.txt", "rb", encoding=5)
except TypeError as e:
    assert str(e) == "open() argument 'encoding' must be str or None, not int", str(e)
else:
    raise AssertionError("expected TypeError")

# unknown encoding is a LookupError at open time
try:
    open("_t236enc.txt", encoding="bogus-codec-xyz")
except LookupError as e:
    assert "unknown encoding" in str(e), str(e)
else:
    raise AssertionError("expected LookupError")

# binary files carry no encoding attribute
fb = open("_t236bad.txt", "rb")
try:
    fb.encoding
except AttributeError:
    pass
else:
    raise AssertionError("expected AttributeError")
fb.close()

# utf-8-sig strips the BOM on read and emits it on write — exactly once,
# like TextIOWrapper's incremental encoder (a bare empty write emits it)
open("_t236sig.txt", "w", encoding="utf-8-sig", newline="").write("hi")
assert open("_t236sig.txt", "rb").read() == b"\xef\xbb\xbfhi"
assert open("_t236sig.txt", encoding="utf-8-sig").read() == "hi"
f = open("_t236sig2.txt", "w", encoding="utf-8-sig", newline="")
f.write("a")
f.write("b")
assert f.tell() == 5, f.tell()
f.close()
assert open("_t236sig2.txt", "rb").read() == b"\xef\xbb\xbfab"
f = open("_t236sig3.txt", "w", encoding="utf-8-sig", newline="")
f.write("")
assert f.tell() == 3, f.tell()
f.write("a")
f.close()
assert open("_t236sig3.txt", "rb").read() == b"\xef\xbb\xbfa"

# utf-16 with BOM roundtrips
open("_t236u16.txt", "w", encoding="utf-16", newline="").write("héllo")
assert open("_t236u16.txt", encoding="utf-16").read() == "héllo"
f = open("_t236u16b.txt", "w", encoding="utf-16", newline="")
f.write("a")
f.write("b")
f.close()
assert open("_t236u16b.txt", "rb").read() == b"\xff\xfea\x00b\x00"
f = open("_t236u16c.txt", "w", encoding="utf-16")
f.write("")
f.close()
assert open("_t236u16c.txt", "rb").read() == b"\xff\xfe"
# the explicit byte-order forms carry no BOM at all
f = open("_t236u16d.txt", "w", encoding="utf-16-le", newline="")
f.write("a")
f.write("b")
f.close()
assert open("_t236u16d.txt", "rb").read() == b"a\x00b\x00"

# latin-1 (byte-exact codec) roundtrips
open("_t236l1.txt", "w", encoding="latin-1", newline="").write("café")
assert open("_t236l1.txt", encoding="latin-1").read() == "café"

# ascii strict encode error through a text write
open("_t236asc.txt", "w", encoding="ascii", newline="").write("ok")
try:
    open("_t236asc.txt", "w", encoding="ascii").write("é")
except UnicodeEncodeError:
    pass
else:
    raise AssertionError("expected UnicodeEncodeError")

# codecs outside the exact-decoding set ride the BCL decoder with the same
# lazy handler resolution; surrogateescape escapes the failing byte and
# resumes cleanly (matching CPython's cp932 behavior byte for byte)
open("_t236g932.txt", "wb").write(b"hi\x81\x39more")
assert open("_t236g932.txt", encoding="cp932", errors="surrogateescape").read() \
    == "hi\udc819more"
f = open("_t236g932.txt", encoding="cp932", errors="totally-bogus-handler")
try:
    f.read()
except LookupError as e:
    assert "unknown error handler" in str(e), str(e)
else:
    raise AssertionError("expected LookupError")
f.close()
f = open("_t236g932.txt", encoding="cp932", errors="namereplace")
try:
    f.read()
except TypeError as e:
    assert "don't know how to handle" in str(e), str(e)
else:
    raise AssertionError("expected TypeError")
f.close()
# a valid file never rejects even an unknown handler on a generic codec
f = open("_t236g932ok.txt", "wb")
f.write(b"plain")
f.close()
f = open("_t236g932ok.txt", encoding="cp932", errors="totally-bogus-handler")
assert f.read() == "plain"
f.close()

# the argument conversions follow CPython's clinic order: the mode string
# and buffering index convert first, then the str-or-None types, and only
# then does the body run its own checks
try:
    open("_t236enc.txt", "zzz", encoding=5)
except TypeError as e:
    assert str(e) == "open() argument 'encoding' must be str or None, not int", str(e)
else:
    raise AssertionError("expected TypeError")
try:
    open("_nonexistent_236.txt", "r", newline=5)
except TypeError as e:
    assert str(e) == "open() argument 'newline' must be str or None, not int", str(e)
else:
    raise AssertionError("expected TypeError")
try:
    open("_nonexistent_236.txt", "r", buffering="x")
except TypeError as e:
    assert str(e) == "'str' object cannot be interpreted as an integer", str(e)
else:
    raise AssertionError("expected TypeError")
# the file argument's own type is validated before the mode is examined
# (CPython body order) — an int is a valid fd there, so use a dict
try:
    open({}, "zzz")
except TypeError as e:
    assert str(e) == "expected str, bytes or os.PathLike object, not dict", str(e)
else:
    raise AssertionError("expected TypeError")
try:
    open({}, "rb", encoding="utf-8")
except TypeError as e:
    assert "expected str, bytes or os.PathLike object" in str(e), str(e)
else:
    raise AssertionError("expected TypeError")

# the newline value is validated before the codec is looked up
open("_t236order.txt", "w").write("x")
try:
    open("_t236order.txt", "r", encoding="bogus-xyz", newline="xx")
except ValueError as e:
    assert str(e) == "illegal newline value: xx", str(e)
else:
    raise AssertionError("expected ValueError")
try:
    open("_t236order.txt", "r", encoding="bogus-xyz", newline="")
except LookupError as e:
    assert "unknown encoding" in str(e), str(e)
else:
    raise AssertionError("expected LookupError")

# seeking a write-mode file disarms the codec preamble unless the final
# position is the very start
f = open("_t236seekw.bin", "wb")
f.close()
f = open("_t236seekw.bin", "w", encoding="utf-8-sig", newline="")
f.seek(3)
f.write("x")
f.close()
assert open("_t236seekw.bin", "rb").read() == b"\x00\x00\x00x"
f = open("_t236seekw2.bin", "w", encoding="utf-8-sig", newline="")
f.write("ab")
f.seek(0)
f.write("Z")
f.close()
assert open("_t236seekw2.bin", "rb").read() == b"\xef\xbb\xbfZb"

# write() returns the character count in code points
f = open("_t236cp.txt", "w", newline="")
assert f.write("ab") == 2
assert f.write("𝄞") == 1, f.write("𝄞")
assert f.write("𝄞𝄞𝄞") == 3
f.close()

# size arguments go through __index__ with CPython's per-method messages
f = open("_t236cp.txt", encoding="utf-8")
assert f.read(None) == "ab𝄞𝄞𝄞𝄞", f.read(None)
f.close()
for method, bad, expected in (
    ("read", 1.0, "argument should be integer or None, not 'float'"),
    ("read", "2", "argument should be integer or None, not 'str'"),
    ("readlines", "x", "argument should be integer or None, not 'str'"),
):
    f = open("_t236cp.txt", encoding="utf-8")
    try:
        getattr(f, method)(bad)
    except TypeError as e:
        assert str(e) == expected, (method, str(e))
    else:
        raise AssertionError("expected TypeError for " + method)
    f.close()
f = open("_t236cp.txt", encoding="utf-8")
try:
    f.readline(1.0)
except TypeError as e:
    assert str(e) == "'float' object cannot be interpreted as an integer", str(e)
else:
    raise AssertionError("expected TypeError")
f.close()
# readlines(hint) counts code points: the astral line is 1 character
open("_t236hint.txt", "w", newline="").write("𝄞\r\nab\ncd\n")
assert len(open("_t236hint.txt").readlines(1)) == 1
assert len(open("_t236hint.txt").readlines(2)) == 2

# buffering converts through a C int: out-of-range overflows
try:
    open("_t236cp.txt", "r", buffering=2**70)
except OverflowError as e:
    assert str(e) == "Python int too large to convert to C int", str(e)
else:
    raise AssertionError("expected OverflowError")

# the core utf-16/32 decoders use CPython 3.14's exact reasons
open("_t236trunc16.bin", "wb").write(b"A\x00\x00")
f = open("_t236trunc16.bin", encoding="utf-16-le")
try:
    f.read()
except UnicodeDecodeError as e:
    assert e.reason == "truncated data", e.reason
else:
    raise AssertionError("expected UnicodeDecodeError")
f.close()
open("_t236sur32.bin", "wb").write(b"\x00\xd8\x00\x00")
f = open("_t236sur32.bin", encoding="utf-32-le")
try:
    f.read()
except UnicodeDecodeError as e:
    assert e.reason == "code point in surrogate code point range(0xd800, 0xe000)", e.reason
else:
    raise AssertionError("expected UnicodeDecodeError")
f.close()

# flag queries raise the no-period closed message after close()
f = open("_t236cp.txt", encoding="utf-8")
f.close()
for method in ("readable", "seekable"):
    try:
        getattr(f, method)()
    except ValueError as e:
        assert str(e) == "I/O operation on closed file", str(e)
    else:
        raise AssertionError("expected ValueError for " + method)
# writable() keeps answering on a closed file, like CPython
assert f.writable() is False
