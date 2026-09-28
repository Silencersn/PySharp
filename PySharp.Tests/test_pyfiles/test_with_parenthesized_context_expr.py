"""A parenthesized context expression followed by `as` is accepted, as are the other with-item forms.

CPython's PEG grammar gives `with_stmt` two productions: a parenthesized item
list (`with (a as x, b):`) and a bare one (`with a as x:`). For a form like
`with (a) as x:` it tries the parenthesized production first, fails to find the
colon after the close paren, and backtracks to the bare production — so the
whole `(a)` is the context expression of a single item. A recursive-descent
parser that commits to the parenthesized production on seeing `(` rejects the
form outright; the cases below are the ones that commitment used to lose.

The accepted set is pinned twice: every form is compiled through compile() so
the fixture itself stays valid, and the interesting ones are executed to check
that the item structure is the one CPython builds (a parenthesized single
expression is one item with that expression, while a parenthesized tuple is a
single item whose context expression is the tuple).

:kind: test
:background: With-item parsing branched on the opening paren and could not go
    back, so `with (expr) as x:` reported "expected ':'" while CPython accepted
    it. Fixed by attempting the parenthesized production and restarting from
    the same token when it fails, mirroring the PEG backtrack.
"""


def expect_syntax_error(src, message):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert e.msg == message, repr(src) + " -> " + repr(e.msg)


def expect_compiles(src):
    compile(src, "<test>", "exec")


# --- the form the issue is about: parenthesized expression + `as` ---

expect_compiles("with (None) as x:\n    pass")
expect_compiles("with (1 + 1) as x:\n    pass")
expect_compiles("with (x) as y:\n    pass")
expect_compiles("with (lambda: None)() as x:\n    pass")
expect_compiles("with (None if True else None) as x:\n    pass")
expect_compiles("with (open('x') if c else open('y')) as f:\n    pass")

# --- parenthesized forms without `as`, and parenthesized item lists ---

expect_compiles("with (None):\n    pass")
expect_compiles("with (None,):\n    pass")
expect_compiles("with ():\n    pass")
expect_compiles("with (a as x):\n    pass")
expect_compiles("with (a as x, b):\n    pass")
expect_compiles("with (a as x, b as y):\n    pass")
expect_compiles("with ((a)) as x:\n    pass")
expect_compiles("with ((a) as x):\n    pass")
expect_compiles("with (a, (b) as c):\n    pass")

# --- deeper context expressions carry their own parse around the parens ---

expect_compiles("with (a := 1) as x:\n    pass")
expect_compiles("with (a for b in c) as x:\n    pass")
expect_compiles("with (a)(b) as x:\n    pass")
expect_compiles("def f():\n    with (yield) as x:\n        pass")
expect_compiles("with (a\n) as x:\n    pass")

# --- a parenthesized tuple is one item whose context expression is the tuple ---

expect_compiles("with (a, b) as x:\n    pass")
expect_compiles("with (a,) as x:\n    pass")
expect_compiles("with (a, b,):\n    pass")

# --- mixed: a parenthesized first item followed by a bare one, and vice versa ---

expect_compiles("with (a) as x, (b) as y:\n    pass")
expect_compiles("with (a), b:\n    pass")
expect_compiles("with (a,) as x, b:\n    pass")
expect_compiles("with a, (b) as y:\n    pass")

# --- async with goes through the same item parsing ---

expect_compiles("async def f():\n    async with (None) as x:\n        pass")
expect_compiles("async def f():\n    async with (a, b):\n        pass")
expect_compiles("async def f():\n    async with (a as x, b as y):\n        pass")
expect_compiles("async def f():\n    async with ((a)) as x:\n        pass")


class CM:
    def __init__(self, tag, log):
        self.tag = tag
        self.log = log

    def __enter__(self):
        self.log.append(("enter", self.tag))
        return self.tag

    def __exit__(self, *exc):
        self.log.append(("exit", self.tag))
        return False


def run_cases():
    log = []

    # a parenthesized single expression is one item: one enter, one exit
    with (CM("g", log)) as x:
        log.append(("body", x))

    # a parenthesized tuple without `as` is still two items, entered in order
    with (CM("t1", log), CM("t2", log)):
        log.append("body2")

    # `with (a) as y, (b) as z` is two items, each parenthesized
    with (CM("l1", log)) as y, (CM("l2", log)) as z:
        log.append(("body3", y, z))

    # nesting redundant parens does not change the structure
    with ((CM("nested", log))) as n:
        log.append(("nested", n))

    # the context expression keeps its own precedence around the parens
    with (CM("cond", log) if True else CM("no", log)) as c:
        log.append(("cond", c))

    return log


assert run_cases() == [
    ("enter", "g"), ("body", "g"), ("exit", "g"),
    ("enter", "t1"), ("enter", "t2"), "body2", ("exit", "t2"), ("exit", "t1"),
    ("enter", "l1"), ("enter", "l2"), ("body3", "l1", "l2"),
    ("exit", "l2"), ("exit", "l1"),
    ("enter", "nested"), ("nested", "nested"), ("exit", "nested"),
    ("enter", "cond"), ("cond", "cond"), ("exit", "cond"),
], run_cases()


# an expression that is not a context manager still parses; it fails at run time
def expect_not_a_context_manager(src):
    try:
        compile(src, "<test>", "exec")
    except SyntaxError as e:
        raise AssertionError(repr(src) + " should compile, got " + repr(e.msg))
    try:
        exec(src, {})
        raise AssertionError(repr(src) + " should raise TypeError")
    except TypeError as e:
        assert "does not support the context manager protocol" in str(e), str(e)


expect_not_a_context_manager("with (None) as x:\n    pass")
expect_not_a_context_manager("with (1 + 1) as x:\n    pass")
expect_not_a_context_manager("with () as x:\n    pass")
expect_not_a_context_manager("with (None, None) as pair:\n    pass")


# a parenthesized tuple bound with `as` receives the tuple, not the items
def tuple_binding():
    try:
        with (None, None) as pair:
            return pair
    except TypeError:
        return "TypeError"


assert tuple_binding() == "TypeError"


# --- async with, driven manually (PySharp has no asyncio) ---

class ACM:
    def __init__(self, tag, log):
        self.tag = tag
        self.log = log

    async def __aenter__(self):
        self.log.append(("aenter", self.tag))
        return self.tag

    async def __aexit__(self, *exc):
        self.log.append(("aexit", self.tag))
        return False


def drive(coro):
    try:
        coro.send(None)
    except StopIteration:
        pass


def run_async_cases():
    log = []

    async def main():
        async with (ACM("g", log)) as x:
            log.append(("body", x))
        async with (ACM("t1", log), ACM("t2", log)):
            log.append("body2")
        async with (ACM("l1", log)) as y, (ACM("l2", log)) as z:
            log.append(("body3", y, z))
        async with ((ACM("nested", log))) as n:
            log.append(("nested", n))

    drive(main())
    return log


assert run_async_cases() == [
    ("aenter", "g"), ("body", "g"), ("aexit", "g"),
    ("aenter", "t1"), ("aenter", "t2"), "body2", ("aexit", "t2"), ("aexit", "t1"),
    ("aenter", "l1"), ("aenter", "l2"), ("body3", "l1", "l2"),
    ("aexit", "l2"), ("aexit", "l1"),
    ("aenter", "nested"), ("nested", "nested"), ("aexit", "nested"),
], run_async_cases()


# --- guards: the forms that are genuinely illegal stay rejected ---

expect_syntax_error("with (a as x) as y:\n    pass", "invalid syntax")
expect_syntax_error("y = (a as x)", "invalid syntax")
expect_syntax_error("with (a; b):\n    pass", "invalid syntax")
expect_syntax_error("with None\n    pass", "expected ':'")
expect_syntax_error("with None as x\n    pass", "expected ':'")
expect_syntax_error("async def f():\n    async with None\n        pass", "expected ':'")


# the message for a missing comma is a separate, pre-existing divergence, so
# only the rejection itself is asserted here
def expect_rejected(src):
    try:
        compile(src, "<test>", "exec")
        raise AssertionError("should raise SyntaxError: " + repr(src))
    except SyntaxError:
        pass


expect_rejected("with (a b):\n    pass")
expect_rejected("with (for):\n    pass")
expect_rejected("with (a,) as x,, b:\n    pass")

print("test_with_parenthesized_context_expr passed")
