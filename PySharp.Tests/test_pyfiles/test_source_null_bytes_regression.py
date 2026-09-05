"""
Regression: a NUL byte anywhere in the source must be rejected with
SyntaxError: source code cannot contain null bytes, like CPython
(Parser/lexer/lexer.c contains_null_bytes, checked per line before
tokenization). PySharp used to silently accept NUL inside string /
bytes literals and comments - and the NUL even entered the string value,
so corrupted data could pass as normal content.

This file constructs the offending sources with chr(0); positions where
PySharp already rejects the source are pinned as guards.
"""

def expect_null_bytes_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert "null bytes" in str(e), str(e)


def expect_rejected(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError:
        pass


# red cases: NUL silently accepted inside literals and comments
expect_null_bytes_error('s = "a\x00b"\n')       # NUL in a str literal
expect_null_bytes_error('b = b"x\x00y"\n')      # NUL in a bytes literal
expect_null_bytes_error('# comment\x00here\n')  # NUL in a comment


# guards: positions where rejection already works must stay rejected
expect_rejected('x = 1\x00\n')                  # NUL after code
expect_rejected('\x00\n')                       # NUL alone on a line

print("test_source_null_bytes_regression passed")
