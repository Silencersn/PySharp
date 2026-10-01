"""The CJK multibyte decoders raise one event per rejected sequence: incomplete multibyte sequence over the remaining bytes when the sequence starting at the error would run past the end of the input, otherwise illegal multibyte sequence pinned to the lead byte alone.

:kind: test
"""
# CPython's cjkcodecs require the whole sequence before consulting the
# mapping (REQUIRE_INBUF), so a lead cut off by the end of the input is the
# incomplete event over every remaining byte, while a rejected sequence
# within the input is reported on its lead byte alone. The event range
# feeds exc.start/exc.end and every errors= handler acts on it.


def decode_error(data, encoding, errors="strict"):
    try:
        data.decode(encoding, errors)
    except UnicodeDecodeError as e:
        assert e.encoding == encoding, (e.encoding, encoding)
        assert e.object == data, (e.object, data)
        return (e.start, e.end, e.reason)
    raise AssertionError("UnicodeDecodeError expected for %r/%s/%s" % (data, encoding, errors))


# a truncated DBCS lead is incomplete, not illegal
assert decode_error(b"\x81", "cp932") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\x81", "shift_jis") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\x81", "gbk") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\x81", "big5") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\x81", "cp949") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\x81", "cp950") == (0, 1, "incomplete multibyte sequence")

# bytes the .NET-style tables map on their own stay leads here: the
# mapping is only consulted once the sequence is complete
assert decode_error(b"\x80", "big5") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\x80", "gbk") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\xff", "gbk") == (0, 1, "incomplete multibyte sequence")

# the incomplete event reaches from the lead to the end of the input
assert decode_error(b"A\x81", "cp932") == (1, 2, "incomplete multibyte sequence")
assert decode_error(b"A\x81", "gbk") == (1, 2, "incomplete multibyte sequence")
assert decode_error(b"\x84\x31\x30", "gb18030") == (0, 3, "incomplete multibyte sequence")
assert decode_error(b"A\x84\x31", "gb18030") == (1, 3, "incomplete multibyte sequence")
assert decode_error(b"\x84", "johab") == (0, 1, "incomplete multibyte sequence")
assert decode_error(b"\xa1\xa1\x81", "gb2312") == (2, 3, "incomplete multibyte sequence")

# a rejected full sequence is illegal and covers the lead byte alone
assert decode_error(b"\x81\x20", "cp932") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x20", "shift_jis") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x20", "gbk") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x20", "big5") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x20", "cp949") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x20", "cp950") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x20", "gb2312") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\x7f", "cp932") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x81\xff", "gbk") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x84\x31\x30\x30", "gb18030") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x90\x01\x41", "gb18030") == (0, 1, "illegal multibyte sequence")
assert decode_error(b"\x84\x40", "johab") == (0, 1, "illegal multibyte sequence")

# decoding stops at the first event
assert decode_error(b"\x81\x20\x81", "cp932") == (0, 1, "illegal multibyte sequence")

# ignore skips the lead byte alone and rescans the trail
assert b"\x81\x20".decode("cp932", "ignore") == " "
assert b"\x81\x20\x81\x20".decode("cp932", "ignore") == "  "
assert b"\x81\x20".decode("cp932", "replace") == "\ufffd "
assert b"\x81\x20".decode("cp932", "backslashreplace") == "\\x81 "
assert b"\x81\x20".decode("gbk", "surrogateescape") == "\udc81 "

# the incomplete tail is one event for the handlers too
assert b"\x81".decode("cp932", "ignore") == ""
assert b"\x81".decode("cp932", "replace") == "\ufffd"
assert b"\x81".decode("cp932", "backslashreplace") == "\\x81"
assert b"\x81".decode("cp932", "surrogateescape") == "\udc81"
assert b"A\x81\x20".decode("gbk", "ignore") == "A "
assert b"A\x81\x20".decode("gbk", "replace") == "A\ufffd "
assert b"\x84\x31\x30".decode("gb18030", "replace") == "\ufffd"

# valid data still decodes across the families
assert b"\x93\xfa\x96{\x8c\xea".decode("cp932") == "日本語"
assert b"\xa5\xa2".decode("cp932") == "･｢"
assert b"\xd6\xd0\xce\xc4".decode("gbk") == "中文"
assert b"\xa4\xa4\xa4\xe5".decode("big5") == "中文"
assert b"\xb0\xaa\xc5\xe9".decode("cp950") == "高體"
assert b"\xc7\xd1\xb1\xb9".decode("cp949") == "한국"
assert b"\xd6\xd0\x82\x32\xd0\x34\xce\xc4".decode("gb18030") == "中䆣文"

print("ok")
