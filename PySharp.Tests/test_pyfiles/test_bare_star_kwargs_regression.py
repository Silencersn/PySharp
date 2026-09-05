"""
Regression: a bare `*` (keyword-only separator) in a parameter list must
be followed by at least one named keyword-only parameter. CPython rejects
the bare-star-without-name forms with
SyntaxError: named arguments must follow bare *
(Parser/parser.c star_kwargs / function parameter rules). PySharp used
to accept and even run all four variants:

    def f1(a, *, **k): ...    # **kwargs cannot satisfy the bare *
    def f2(*, ): ...          # bare * + trailing comma
    g1 = lambda *, : 1        # lambda family
    g2 = lambda *, **k: ...   # lambda family with **kwargs

Each illegal form is checked through compile() so this file itself stays
valid. Guards pin the adjacent validations that already match CPython:
legal signatures keep compiling, the double bare * stays rejected, and a
legal keyword-only call keeps working.
"""


def expect_syntax_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert "must follow bare *" in str(e), str(e)


# red cases: bare * with no named keyword-only parameter after it
expect_syntax_error("def f1(a, *, **k):\n    return (a, k)\n")
expect_syntax_error("def f2(*, ):\n    pass\n")
expect_syntax_error("g1 = lambda *, : 1\n")
expect_syntax_error("g2 = lambda *, **k: (1, k)\n")


# guards: forms that already match CPython
compile("def f(*args, **k):\n    pass\n", "<test>", "exec")      # named *args
compile("def f(a, *, b):\n    pass\n", "<test>", "exec")         # named kw-only
compile("def f(a, *, b=2, **k):\n    pass\n", "<test>", "exec")  # full form

try:
    compile("def f(*, *, x):\n    pass\n", "<test>", "exec")
    assert False, "double bare * should raise SyntaxError"
except SyntaxError:
    pass

ns = {}
exec("def f(a, *, b):\n    return (a, b)\n", ns)
assert ns["f"](1, b=2) == (1, 2)

print("test_bare_star_kwargs_regression passed")
