"""
Regression: a decimal integer literal with more than 4300 digits must be
rejected with SyntaxError (the compile-time half of the CVE-2020-10735
integer-string-conversion guard), like CPython. PySharp used to accept
and evaluate arbitrarily long decimal literals.

CPython 3.14 reference (Parser/pegen.c _PyPegen_number_token via
PyLong_FromString length limit; Objects/longobject.c
_MAX_STR_DIGITS_ERROR_FMT_TO_INT):
    x = <4300 digits>   -> fine
    x = <4301 digits>   -> SyntaxError: Exceeds the limit (4300 digits)

The digit count excludes underscores, and power-of-two bases (0x/0b/0o)
are not limited - both kept below as guards.
"""

def compile_ok(src):
    compile(src, "<test>", "exec")


def expect_syntax_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError (int literal too long)"
    except SyntaxError:
        pass


# red case: exactly one digit over the limit
expect_syntax_error("x = " + "1" * 4301)

# boundary: 4300 digits is allowed, and the parsed value must be correct
digits = "1" * 4300
compile_ok("x = " + digits)
ns = {}
exec("y = " + digits, ns)
y = ns["y"]
assert isinstance(y, int)
assert y % 9 == 7  # digit sum of 4300 ones, mod 9

# guards: digit count ignores underscores; hex is unlimited
compile_ok("_".join(["1"] * 2200))  # 4399 chars but only 2200 digits
compile_ok("0x" + "f" * 5000)

print("test_int_literal_max_digits_regression passed")
