"""Verifies bytes.decode error-handler behavior: encode-only handlers reject decode errors with TypeError, surrogateescape escapes only bytes >= 0x80 and stops at ASCII, utf-16/utf-32 hand lone surrogate units to surrogatepass instead of raw errors, and unknown handler names stay lazy until the first actual error.

Asserts CPython 3.14 reference values and exact UnicodeDecodeError reasons for each face.

:kind: test
"""

# bytes.decode error-handler behavior: the handlers CPython registers for
# encoding alone reject a decode error, surrogateescape only escapes bytes
# >= 0x80 and never one that starts on an ASCII byte, the utf-16/utf-32
# decoders hand a lone surrogate unit to surrogatepass instead of a raw
# .NET exception, and an unknown handler name stays inert while the bytes
# decode (CPython resolves it at the first actual error).
#
# CPython 3.14 reference:
#     b'\xff'.decode('utf-8', 'namereplace')  -> TypeError: don't know how to
#         handle UnicodeDecodeError in error callback
#     b'\xf0\x9f'.decode('utf-8', 'surrogateescape') == '\udcf0\udc9f'
#     b'A'.decode('utf-16-le', 'surrogateescape')  -> UnicodeDecodeError
#     b'\x00\xd8\x00\x00'.decode('utf-32-le', 'surrogatepass') == '\ud800'
#     b'abc'.decode('cp932', 'bogus') == 'abc'

# --- handlers registered for encoding only -------------------------------

for handler in ("namereplace", "xmlcharrefreplace"):
    try:
        b"\xff".decode("utf-8", handler)
    except UnicodeDecodeError:
        raise AssertionError(f"{handler}: UnicodeDecodeError must not escape")
    except TypeError as exc:
        assert str(exc) == (
            "don't know how to handle UnicodeDecodeError in error callback"
        ), str(exc)
    else:
        raise AssertionError(f"TypeError expected for {handler}")

# --- surrogateescape escape rule ----------------------------------------

assert b"\xff".decode("utf-8", "surrogateescape") == "\udcff"
assert b"\xff\xff\xff\xff\xff".decode("utf-8", "surrogateescape") == "\udcff" * 5
assert b"\x80".decode("utf-8", "surrogateescape") == "\udc80"
# a truncated sequence escapes every byte of the error
assert b"\xf0\x9f".decode("utf-8", "surrogateescape") == "\udcf0\udc9f"
# the run stops at the first ASCII byte, which then decodes normally
assert b"\xc3\x28".decode("utf-8", "surrogateescape") == "\udcc3("
assert b"\xffA\xff".decode("utf-8", "surrogateescape") == "\udcffA\udcff"

# a lone surrogate unit is not an escaped byte: the codec's own error stands
def decode_error(data, encoding, errors):
    try:
        data.decode(encoding, errors)
    except UnicodeDecodeError as exc:
        return exc
    raise AssertionError(f"{data!r}.decode({encoding!r}, {errors!r}) did not raise")


exc = decode_error(b"\x00\xd8", "utf-16-le", "surrogateescape")
assert exc.reason == "unexpected end of data", exc.reason
exc = decode_error(b"A\x00\x00\xd8", "utf-16-le", "surrogateescape")
assert (exc.start, exc.end) == (2, 4), (exc.start, exc.end)
exc = decode_error(b"\x00\xd8\x00\x00", "utf-32-le", "surrogateescape")
assert exc.reason == "code point in surrogate code point range(0xd800, 0xe000)", exc.reason

# an escaped byte at the very end still works
assert b"\x80".decode("utf-16-le", "surrogateescape") == "\udc80"

# --- utf-32: surrogatepass and the error reasons --------------------------

assert b"\x00\xd8\x00\x00".decode("utf-32-le", "surrogatepass") == "\ud800"
assert b"\x00\xdc\x00\x00".decode("utf-32-le", "surrogatepass") == "\udc00"
assert b"\x00\xd8".decode("utf-16-le", "surrogatepass") == "\ud800"

exc = decode_error(b"\x00\xdc\x00\x00", "utf-32-le", "strict")
assert exc.reason == "code point in surrogate code point range(0xd800, 0xe000)", exc.reason
exc = decode_error(b"\xff\xff\xff\xff", "utf-32-le", "strict")
assert exc.reason == "code point not in range(0x110000)", exc.reason
assert (exc.start, exc.end) == (0, 4), (exc.start, exc.end)
exc = decode_error(b"A\x00\x00", "utf-32-le", "strict")
assert exc.reason == "truncated data", exc.reason

# valid astral code points keep decoding
assert b"\x00\xf6\x01\x00".decode("utf-32-le") == "\U0001f600"

# --- lazy handler resolution on a codec decoded through .NET -------------

assert b"abc".decode("cp932", "bogus") == "abc"
assert b"abc".decode("cp932", "ignore") == "abc"
try:
    b"\x81".decode("cp932", "bogus")
except LookupError as exc:
    assert str(exc) == "unknown error handler name 'bogus'", str(exc)
else:
    raise AssertionError("LookupError expected")

# the failing codec reports its own registry name
exc = decode_error(b"\x81", "cp932", "strict")
assert exc.encoding == "cp932", exc.encoding
assert (exc.start, exc.end) == (0, 1), (exc.start, exc.end)

print("test_bytes_decode_error_handlers passed")
