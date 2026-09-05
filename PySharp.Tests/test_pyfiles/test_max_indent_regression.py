"""
Regression: more than 100 levels of indentation must be rejected with
IndentationError ("too many levels of indentation"), like CPython's
MAXINDENT=100 lexer limit. PySharp used to accept any indentation depth.

CPython 3.14 reference (Parser/lexer/state.h MAXINDENT,
Parser/pegen_errors.c:108):
    99 nested "if True:" levels  -> fine
    100 nested "if True:" levels -> IndentationError: too many levels of
                                    indentation

The boundary is pinned at exactly 100 (rejected) / 99 (accepted, and the
nested body must still execute correctly).
"""

def make_nested_ifs(levels):
    lines = [" " * (4 * i) + "if True:" for i in range(levels)]
    lines.append(" " * (4 * levels) + "x = " + str(levels))
    return "\n".join(lines) + "\n"


def expect_indentation_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise IndentationError"
    except SyntaxError as e:
        # IndentationError is a SyntaxError subclass; require the CPython
        # message so an unrelated syntax error cannot satisfy this check
        assert "too many levels" in str(e), str(e)


# red case: exactly at the CPython boundary (100th level must be rejected)
expect_indentation_error(make_nested_ifs(100))

# guard: 99 levels must stay legal and the nested body must execute
ns = {}
exec(make_nested_ifs(99), ns)
assert ns["x"] == 99

print("test_max_indent_regression passed")
