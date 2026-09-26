"""A compound statement header that ends without a colon is rejected with "expected ':'".

Every place Python expects a colon to close a compound statement header reports
the same missing token, whether the header opens a statement (`if`, `for`,
`while`, `def`, `class`, `try`, `with`, `match`, and their `async` forms) or a
clause of one (`elif`, `else`, `except`, `except*`, `finally`, `case`). Each
form below is compiled through compile() so this file itself stays valid.

Expression-level colons are a different expectation and must not be swept up by
this message: dict entries, slices, annotations and lambda bodies keep their own
diagnostics. The guards at the bottom pin that separation, along with the fact
that a legal header still compiles.

:kind: test
:background: These headers used to fall through to the generic "invalid syntax"
    while already carrying the right line and column, unlike CPython, which names
    the missing token for every one of them.
"""


def expect_missing_colon(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert e.msg == "expected ':'", repr(src) + " -> " + repr(e.msg)


# statement headers
expect_missing_colon("if True\n    x = 1\n")
expect_missing_colon("for i in [1]\n    pass\n")
expect_missing_colon("while True\n    pass\n")
expect_missing_colon("def f()\n    pass\n")
expect_missing_colon("class C\n    pass\n")
expect_missing_colon("class C()\n    pass\n")
expect_missing_colon("try\n    pass\nexcept ValueError\n    pass\n")
expect_missing_colon("with None\n    pass\n")
expect_missing_colon("with None as x\n    pass\n")
expect_missing_colon("match x:\n    case 1\n        pass\n")

# async statement headers
expect_missing_colon("async def g()\n    pass\n")
expect_missing_colon("async def f():\n    async for i in a\n        pass\n")
expect_missing_colon("async def f():\n    async with None\n        pass\n")

# clause headers
expect_missing_colon("if False:\n    pass\nelif True\n    pass\n")
expect_missing_colon("if False:\n    pass\nelse\n    pass\n")
expect_missing_colon("for i in [1]:\n    pass\nelse\n    pass\n")
expect_missing_colon("while True:\n    pass\nelse\n    pass\n")
expect_missing_colon("try:\n    pass\nexcept ValueError\n    pass\n")
expect_missing_colon("try:\n    pass\nexcept* TypeError\n    pass\n")
expect_missing_colon("try:\n    pass\nfinally\n    pass\n")
expect_missing_colon("try:\n    pass\nexcept ValueError:\n    pass\nelse\n    pass\n")

# a header with nothing after it is the same failure
expect_missing_colon("if True")


def expect_other_syntax_error(src, fragment):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        # an expression-level colon is a different expectation, so it must not
        # borrow the compound-header message
        assert fragment in e.msg, repr(src) + " -> " + repr(e.msg)


# guards: expression-level colons keep their own diagnostics
expect_other_syntax_error("x = [1, 2, 3]\nx[1 2]\n", "invalid syntax")
expect_other_syntax_error("d = {1 2}\n", "invalid syntax")
expect_other_syntax_error("1: int = 2\n", "illegal target for annotation")
expect_other_syntax_error("match v:\n    case {'a' 1}:\n        pass\n", "invalid syntax")

# guards: legal headers still compile
compile("if True:\n    pass\n", "<test>", "exec")
compile("for i in [1]:\n    pass\nelse:\n    pass\n", "<test>", "exec")
compile("def f(a, *, b=1) -> int:\n    return a\n", "<test>", "exec")
compile("async def f():\n    async with None as x:\n        pass\n", "<test>", "exec")
compile("try:\n    pass\nexcept* TypeError:\n    pass\n", "<test>", "exec")

print("test_missing_colon_syntax_error passed")
