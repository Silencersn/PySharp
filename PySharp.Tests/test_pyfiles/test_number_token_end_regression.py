"""
Regression: the lexer must validate the end of a number token like
CPython's verify_end_of_number (Parser/lexer/lexer.c:304-358):

1. `0or 1`: the `0o` prefix is committed but no octal digit follows, so
   CPython raises SyntaxError: invalid octal literal. PySharp's regex
   alternation used to fall back to the decimal branch and re-tokenize
   the input as `0 or 1`, silently executing it.
2. A complete number directly followed by one of
   and/else/for/if/in/is/or (1or, 0.0or, 0jor, 0b0or, ...) must emit a
   SyntaxWarning "invalid <type> literal" while still evaluating; the
   values below are correct on both interpreters, and the C# side
   (TestNumberTokenEndValidationRegression) captures stderr to assert
   the warnings actually appear.

Guards: the pure-prefix error forms (0band / 0xor / 1eor / 0Or) are
rejected by both interpreters (message wording differs today), and
normal numeric literals are unaffected.
"""

def expect_syntax_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert "invalid octal literal" in str(e), str(e)


# red case: the 0o prefix is committed, `0or` cannot fall back to `0 or`
expect_syntax_error("x = 0or 1\n")
expect_syntax_error("print(0or 1)\n")


# warning forms: must evaluate to 1 AND each must emit a SyntaxWarning
a = 0o1or 1
assert a == 1
b = 1or 1
assert b == 1
c = 0.0or 1
assert c == 1
d = 0jor 1
assert d == 1
e = 0b0or 1
assert e == 1


# guards: pure-prefix errors already rejected on both sides
def rejected(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError:
        pass


rejected("x = 0band 1\n")
rejected("x = 0xor 1\n")
rejected("x = 1eor 1\n")
rejected("x = 0Or 1\n")

# normal numeric literals are unaffected
assert (0o17, 0b101, 0xff) == (15, 5, 255)

print("test_number_token_end_regression passed")
