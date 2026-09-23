# Regression: a failed conversion quotes its argument with %R of the original
# object, not by pasting the raw value between two hard-coded quotes. CPython
# formats float failures with %R (float_from_string_inner,
# Objects/floatobject.c:162, reached through _Py_string_to_number_with_
# underscores with the same object) and int failures with %.200R
# (PyLong_FromUnicodeObject, Objects/longobject.c:3126), so control
# characters, quotes and backslashes arrive as Python literal escapes, an
# overridden __repr__ is the one that runs, and the int messages additionally
# cap the rendered repr at 200 characters — which cuts the closing quote off.
# A bytes-like argument is an exception: CPython builds a fresh bytes from the
# same data (_PyLong_FromBytes, Objects/longobject.c:3093), so a bytes
# subclass repr never shows up there.

INT10 = "invalid literal for int() with base 10: "
INT16 = "invalid literal for int() with base 16: "
INT2 = "invalid literal for int() with base 2: "
INT0 = "invalid literal for int() with base 0: "
FLOAT = "could not convert string to float: "
BACKSLASH = chr(92)


def expect(label, fn, exc, msg):
    try:
        fn()
    except exc as e:
        assert str(e) == msg, f"{label}: {str(e)!r} != {msg!r}"
    else:
        assert False, f"{label}: no {exc.__name__}"


class ReprStr(str):
    def __repr__(self):
        return "<ReprStr marker>"


class ReprBytes(bytes):
    def __repr__(self):
        return "<ReprBytes marker>"


# --- control characters, U+200B, single quote, backslash: the repr escapes
# --- that the raw-value rendering used to leak as unprintable bytes
expect("int.c1c", lambda: int('\x1c123'), ValueError, INT10 + "'\\x1c123'")
expect("int.zwsp", lambda: int('\u200b123'), ValueError, INT10 + "'\\u200b123'")
expect("int.quote", lambda: int("'1"), ValueError, INT10 + '"\'1"')
expect("int.bslash", lambda: int('1' + BACKSLASH), ValueError, INT10 + "'1" + BACKSLASH * 2 + "'")
expect("int.nul", lambda: int('12\x003'), ValueError, INT10 + "'12\\x003'")
expect("int.tab", lambda: int('a\tb'), ValueError, INT10 + "'a\\tb'")
expect("int.base16.c1c", lambda: int('\x1c1', 16), ValueError, INT16 + "'\\x1c1'")
expect("int.base16.bslash", lambda: int('1' + BACKSLASH, 16), ValueError, INT16 + "'1" + BACKSLASH * 2 + "'")
expect("int.base2.quote", lambda: int("'", 2), ValueError, INT2 + '"\'"')
expect("int.base0.quote", lambda: int("'", 0), ValueError, INT0 + '"\'"')
expect("float.c1c", lambda: float('\x1c123'), ValueError, FLOAT + "'\\x1c123'")
expect("float.zwsp", lambda: float('\u200b123'), ValueError, FLOAT + "'\\u200b123'")
expect("float.quote", lambda: float("'1"), ValueError, FLOAT + '"\'1"')
expect("float.bslash", lambda: float('1' + BACKSLASH), ValueError, FLOAT + "'1" + BACKSLASH * 2 + "'")
expect("float.nul", lambda: float('\x00'), ValueError, FLOAT + "'\\x00'")
expect("float.tab", lambda: float('a\tb'), ValueError, FLOAT + "'a\\tb'")

# --- the repr of the original object is what runs, overridden __repr__ included
expect("int.subclass", lambda: int(ReprStr('abc')), ValueError, INT10 + "<ReprStr marker>")
expect("float.subclass", lambda: float(ReprStr('abc')), ValueError, FLOAT + "<ReprStr marker>")

# --- %.200R caps the rendered repr at 200 characters; a message that hits the
# --- cap ends without the closing quote, one that exactly fits keeps it
expect("int.cap.fits", lambda: int('x' * 198), ValueError, INT10 + "'" + 'x' * 198 + "'")
expect("int.cap.cut", lambda: int('x' * 199), ValueError, INT10 + "'" + 'x' * 199)
expect("int.cap.far", lambda: int('x' * 300), ValueError, INT10 + "'" + 'x' * 199)
expect("int.cap.base16", lambda: int('x' * 300, 16), ValueError, INT16 + "'" + 'x' * 199)
try:
    int('x' * 300)
except ValueError as e:
    assert len(str(e)) == 240, len(str(e))
    assert str(e).endswith('x'), "the cap must cut the closing quote off"
else:
    assert False, "int.cap: no ValueError"

# --- bytes-like arguments quote a fresh bytes built from the same data, so the
# --- cap applies to its repr (b' + 49 escapes + a cut escape = 200 characters)
expect("int.bytes.ff", lambda: int(b'1\xff'), ValueError, INT10 + "b'1\\xff'")
expect("int.bytes.quote", lambda: int(b"'1"), ValueError, INT10 + 'b"\'1"')
expect("int.bytes.empty", lambda: int(b''), ValueError, INT10 + "b''")
expect("int.bytes.subclass", lambda: int(ReprBytes(b'abc')), ValueError, INT10 + "b'abc'")
expect("int.bytes.cap", lambda: int(b'\xff' * 300), ValueError,
       INT10 + "b'" + "\\xff" * 49 + "\\x")
expect("int.bytearray", lambda: int(bytearray(b'1.5')), ValueError, INT10 + "b'1.5'")
expect("int.memoryview", lambda: int(memoryview(b'1.5')), ValueError, INT10 + "b'1.5'")

# --- ...but the float messages carry no precision, so theirs is never cut
expect("float.cap.fits", lambda: float('x' * 198), ValueError, FLOAT + "'" + 'x' * 198 + "'")
expect("float.cap.far", lambda: float('x' * 300), ValueError, FLOAT + "'" + 'x' * 300 + "'")
try:
    float('x' * 300)
except ValueError as e:
    assert len(str(e)) == 337, len(str(e))
    assert str(e).endswith("'"), "the float message must keep the whole repr"
else:
    assert False, "float.cap: no ValueError"

# --- the neighbouring strings stay uncapped and unescaped as CPython leaves them
expect("float.space", lambda: float('   '), ValueError, FLOAT + "'   '")
expect("float.empty", lambda: float(''), ValueError, FLOAT + "''")
expect("complex.quote", lambda: complex("'1j"), ValueError, "complex() arg is a malformed string")
expect("float.fromhex", lambda: float.fromhex('0x1p'), ValueError,
       "invalid hexadecimal floating-point string")
