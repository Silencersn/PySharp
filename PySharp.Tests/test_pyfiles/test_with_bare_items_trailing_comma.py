"""The bare with-item list rejects a trailing comma, pointing at the colon.

CPython's with_stmt has two productions with different comma allowances: the
parenthesized item list permits an optional trailing comma (','?  before ')'),
while the bare one is ','.with_item+ ':' with no such allowance. So
`with (a,):` is legal but `with a,:` is not — and since a form like
`with (a),:` reaches the bare production after the parenthesized one
backtracks, it is rejected too. The rejection lands on the colon token: after
the comma the parser needs a third with_item and the colon cannot start one.

:kind: test
:background: The bare with-item branch discarded the trailing-comma token the
    shared list helper reports to its callers, so `with a,:`, `with a, b,:`
    and `with (a),:` were accepted and executed where CPython 3.14 rejects
    them at compile time with "invalid syntax" on the colon.
"""


def expect_syntax_error(src, offset, end_offset):
    try:
        compile(src, "<test>", "exec")
        raise AssertionError("should raise SyntaxError: " + repr(src))
    except SyntaxError as e:
        assert e.msg == "invalid syntax", repr(src) + " -> " + repr(e.msg)
        assert (e.offset, e.end_offset) == (offset, end_offset), \
            repr(src) + " -> " + repr((e.offset, e.end_offset))


# --- the bare form: comma directly before the terminating colon ---

expect_syntax_error("with a,:", 8, 9)
expect_syntax_error("with a, b,:", 11, 12)
expect_syntax_error("with a, b, :", 12, 13)
expect_syntax_error("with a as x,:", 13, 14)

# --- the parenthesized production backtracks into the bare one ---

expect_syntax_error("with (a),:", 10, 11)
expect_syntax_error("with (a,),:", 11, 12)

# --- async with shares the item parsing ---

expect_syntax_error("async with a,:", 14, 15)
expect_syntax_error("async with (a),:", 16, 17)

# --- a doubled comma keeps blaming the token that cannot start an item ---

expect_syntax_error("with a,, b:", 8, 9)

# --- the parenthesized form keeps its optional trailing comma ---

compile("with (a,):\n    pass", "<test>", "exec")
compile("with (a, b,):\n    pass", "<test>", "exec")
compile("with (a,) as x:\n    pass", "<test>", "exec")

# --- legal bare forms stay legal ---

compile("with a, b:\n    pass", "<test>", "exec")
compile("with (a), b:\n    pass", "<test>", "exec")
compile("with a as x, b as y:\n    pass", "<test>", "exec")

# a comma not followed by the colon still fails through the item parse
expect_syntax_error("with a,", 8, 9)

print("test_with_bare_items_trailing_comma passed")
