"""
Regression: a bytes literal containing a non-ASCII character (any code
point above 0x7F) must be rejected with
SyntaxError: bytes can only contain ASCII literal characters
(CPython Parser/string_parser.c:328), for plain and raw prefixes alike.

PySharp used to split into two wrong behaviors:
- U+0080 to U+00FF (Latin-1 range): silently accepted, value = the low
  byte of the code point;
- U+0100 and above: reported through the unrelated unicodeescape codec
  error path instead.

This file stays pure ASCII; the offending sources are built with
unicode escape sequences and fed to compile().
"""

def expect_ascii_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert "bytes can only contain ASCII" in str(e), str(e)


# red: Latin-1 range silently accepted today
expect_ascii_error('x = b"\u00e9"')        # U+00E9
expect_ascii_error('x = b"\u00ff"')        # U+00FF, upper boundary
expect_ascii_error('x = rb"\u00e4"')       # raw prefix, same rule
expect_ascii_error('x = b"\\xff\u00e4z"')  # legal escape + non-ASCII mix

# red: U+0100 and above must take the same message, not the
# unicodeescape codec error path
expect_ascii_error('x = b"\u0100"')        # U+0100, lower boundary
expect_ascii_error('x = rb"\u20ac"')       # U+20AC


# guards: escape sequences and ASCII are legal bytes content
x = b"\xff"
assert x == b"\xff" and x[0] == 255
assert b"ascii" == b"ascii"

print("test_bytes_literal_ascii_regression passed")
