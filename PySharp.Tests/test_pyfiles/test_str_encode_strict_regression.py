# str.encode must be strict by default: a code point the codec cannot
# represent raises UnicodeEncodeError instead of being replaced by '?' or
# U+FFFD, and the error carries CPython's codec name, code-point range and
# wording. The error handlers registered for surrogateescape/surrogatepass
# are part of the same table.
#
# CPython 3.14 reference:
#     chr(0xD800).encode('utf-8')
#       UnicodeEncodeError: 'utf-8' codec can't encode character '\ud800'
#       in position 0: surrogates not allowed
#     '中'.encode('ascii')
#       UnicodeEncodeError: 'ascii' codec can't encode character '\u4e2d'
#       in position 0: ordinal not in range(128)
#     '中中' + chr(0xD800) encoded as ascii covers position 0-2 (a run)
#     chr(0xD800).encode('utf-8', 'surrogatepass') == b'\xed\xa0\x80'
#     '\udc80'.encode('utf-8', 'surrogateescape') == b'\x80'

HI = chr(0xD800)
LO = chr(0xDC80)
ZH = '\u4e2d'

# --- strict raises, with CPython's message -------------------------------


def raises(text, encoding, errors=None, name="UnicodeEncodeError"):
    try:
        if errors is None:
            text.encode(encoding)
        else:
            text.encode(encoding, errors)
    except UnicodeEncodeError as exc:
        assert type(exc).__name__ == name, type(exc).__name__
        return exc
    raise AssertionError(f"{text!r}.encode({encoding!r}) did not raise")


exc = raises(HI, "utf-8")
assert str(exc) == (
    "'utf-8' codec can't encode character '\\ud800' in position 0: surrogates not allowed"
), str(exc)
assert exc.encoding == "utf-8" and exc.start == 0 and exc.end == 1, exc.args
assert exc.reason == "surrogates not allowed", exc.reason
assert exc.object == HI, repr(exc.object)

exc = raises(ZH, "ascii")
assert str(exc) == (
    "'ascii' codec can't encode character '\\u4e2d' in position 0: ordinal not in range(128)"
), str(exc)
assert exc.reason == "ordinal not in range(128)", exc.reason

exc = raises(ZH, "latin-1")
assert exc.encoding == "latin-1" and exc.reason == "ordinal not in range(256)", exc.args

# the generic single byte codecs report themselves as 'charmap'
exc = raises(ZH, "cp1252")
assert exc.encoding == "charmap", exc.encoding
assert exc.reason == "character maps to <undefined>", exc.reason

# a codec without a hand-rolled encoder reports its own registry name
exc = raises("\u0e01", "shift_jis")
assert exc.encoding == "shift_jis", exc.encoding
assert exc.reason == "illegal multibyte sequence", exc.reason

# UnicodeEncodeError is a UnicodeError and a ValueError
assert issubclass(UnicodeEncodeError, UnicodeError)
assert issubclass(UnicodeError, ValueError)
try:
    ZH.encode("ascii")
except ValueError as caught:
    assert isinstance(caught, UnicodeEncodeError)
else:
    raise AssertionError("ValueError expected")

# --- the failure covers the run of unmappable code points ----------------

exc = raises(ZH + ZH + HI, "ascii")
assert (exc.start, exc.end) == (0, 3), (exc.start, exc.end)
assert str(exc) == (
    "'ascii' codec can't encode characters in position 0-2: ordinal not in range(128)"
), str(exc)

exc = raises("a" + HI + "b", "utf-8")
assert (exc.start, exc.end) == (1, 2), (exc.start, exc.end)

# utf-8 collects consecutive surrogates, utf-16 the same input reports one unit
exc = raises(HI + HI, "utf-8")
assert (exc.start, exc.end) == (0, 2), (exc.start, exc.end)
exc = raises(HI + HI, "utf-16")
assert (exc.start, exc.end) == (0, 1), (exc.start, exc.end)

# --- the handlers that were missing -------------------------------------

assert HI.encode("utf-8", "surrogatepass") == b"\xed\xa0\x80"
assert LO.encode("utf-8", "surrogatepass") == b"\xed\xb2\x80"
assert HI.encode("utf-16", "surrogatepass") == b"\xff\xfe\x00\xd8"
assert HI.encode("utf-16-be", "surrogatepass") == b"\xd8\x00"
assert HI.encode("utf-32-le", "surrogatepass") == b"\x00\xd8\x00\x00"
# surrogatepass only knows the standard encodings
exc = raises(HI, "ascii", "surrogatepass")
assert exc.reason == "ordinal not in range(128)", exc.reason
exc = raises(HI, "cp1252", "surrogatepass")
assert exc.encoding == "charmap", exc.encoding

assert LO.encode("utf-8", "surrogateescape") == b"\x80"
assert LO.encode("ascii", "surrogateescape") == b"\x80"
assert LO.encode("latin-1", "surrogateescape") == b"\x80"
assert b"\x80".decode("utf-8", "surrogateescape") == LO
# a high surrogate is not an escaped byte, and neither is a normal character
exc = raises(HI, "utf-8", "surrogateescape")
assert exc.reason == "surrogates not allowed", exc.reason
exc = raises(ZH, "ascii", "surrogateescape")
assert exc.reason == "ordinal not in range(128)", exc.reason
# one escaped byte cannot fill a utf-16/utf-32 code unit
exc = raises(LO, "utf-16", "surrogateescape")
assert exc.reason == "surrogates not allowed", exc.reason
exc = raises(LO, "utf-32-le", "surrogateescape")
assert exc.reason == "surrogates not allowed", exc.reason

# --- the successful paths keep working ----------------------------------

assert "abc".encode("utf-8") == b"abc"
assert "".encode("utf-16") == b"\xff\xfe"
assert "ab".encode("utf-32") == b"\xff\xfe\x00\x00a\x00\x00\x00b\x00\x00\x00"
assert "\U0001f600".encode("utf-8") == b"\xf0\x9f\x98\x80"
assert (ZH + HI).encode("ascii", "backslashreplace") == b"\\u4e2d\\ud800"
assert (ZH + HI).encode("ascii", "xmlcharrefreplace") == b"&#20013;&#55296;"
assert (ZH + HI).encode("ascii", "ignore") == b""
assert (ZH + HI).encode("ascii", "replace") == b"??"
# the replacement text is itself encoded in the codec's byte order
assert (ZH + HI).encode("utf-16", "replace") == b"\xff\xfe-N?\x00"

print("test_str_encode_strict_regression passed")
