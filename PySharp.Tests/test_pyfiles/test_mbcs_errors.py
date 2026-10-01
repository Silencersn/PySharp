"""The mbcs codec reports every strict failure at position 0-0 with the wording that start == end implies, and its decode reason is the Windows code page message.

:kind: test
"""
# CPython's code page converters never locate the failing unit:
# encode_code_page_errors and decode_code_page_errors raise the strict
# exception with start == end == 0 no matter where the error is, so the
# message takes the start + 1 != end branch ("characters/bytes in
# position 0--1"). The decode reason is the hard-coded Windows text.
# This corpus assumes mbcs == UTF-8 (ANSI code page 65001), which is
# what the .NET-side mbcs maps to.


def encode_error(value, errors="strict"):
    try:
        value.encode("mbcs", errors)
    except UnicodeEncodeError as e:
        assert e.encoding == "mbcs", e.encoding
        assert e.object == value, (e.object, value)
        assert e.reason == "invalid character", e.reason
        return (e.start, e.end)
    raise AssertionError("UnicodeEncodeError expected for %r" % value)


def decode_error(data, errors="strict"):
    try:
        data.decode("mbcs", errors)
    except UnicodeDecodeError as e:
        assert e.encoding == "mbcs", e.encoding
        assert e.object == data, (e.object, data)
        assert e.reason == "No mapping for the Unicode character exists in the target code page.", e.reason
        return (e.start, e.end)
    raise AssertionError("UnicodeDecodeError expected for %r" % data)


# the strict range is zero-length at position 0 regardless of where the
# unencodable character or undecodable byte actually sits
assert encode_error("\ud800") == (0, 0)
assert encode_error("aé\ud800") == (0, 0)
assert encode_error("ab\ud800c") == (0, 0)
assert decode_error(b"\xed\xa0\x80") == (0, 0)
assert decode_error(b"\xc3\xa9\xff") == (0, 0)
assert decode_error(b"a\xffb") == (0, 0)

# the error handlers keep the per-unit events and positions
assert "\ud800".encode("mbcs", "replace") == b"?"
assert "\ud800".encode("mbcs", "ignore") == b""
assert "ab\ud800c".encode("mbcs", "replace") == b"ab?c"
assert "ab\ud800c".encode("mbcs", "backslashreplace") == b"ab\\ud800c"
try:
    "\ud800".encode("mbcs", "surrogateescape")
except UnicodeEncodeError as e:
    assert (e.start, e.end) == (0, 1), (e.start, e.end)
else:
    raise AssertionError("UnicodeEncodeError expected")

assert b"\xed\xa0\x80".decode("mbcs", "replace") == "\ufffd\ufffd\ufffd"
assert b"\xed\xa0\x80".decode("mbcs", "ignore") == ""
assert b"\xed\xa0\x80".decode("mbcs", "surrogateescape") == "\udced\udca0\udc80"
assert b"\xed\xa0\x80".decode("mbcs", "backslashreplace") == "\\xed\\xa0\\x80"
assert b"a\xffb".decode("mbcs", "replace") == "a\ufffdb"

# valid data still round-trips
assert "中文".encode("mbcs") == b"\xe4\xb8\xad\xe6\x96\x87"
assert b"\xe4\xb8\xad\xe6\x96\x87".decode("mbcs") == "中文"

print("ok")
