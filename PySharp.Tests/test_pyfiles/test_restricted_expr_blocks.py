"""Restricted expression positions reject yield/walrus/await at compile time.

CPython's symbol table walks every lazily-evaluated position inside a
restricted block type (symtable_raise_if_annotation_block): the bound, default
or constraint of each PEP 695 type parameter, the value of a type alias, and
every annotation. A yield, named expression or await found in one is a
SyntaxError whose message names both the expression kind and the block.

Nested scopes lift the restriction the way CPython's block stack does: a
lambda's defaults evaluate in the current block while its body is a function
block of its own, and a comprehension is a block of its own.

:kind: test
:background: symtable.c raises through symtable_raise_if_annotation_block for
    NamedExpr/Yield/YieldFrom/Await; the semantic analyzer had no notion of
    restricted blocks, so all of these positions compiled and ran.
"""


def expect_syntax_error(src, message, lineno=1):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert e.msg == message, repr(src) + " -> " + repr(e.msg)
        assert e.lineno == lineno, repr(src) + " -> lineno " + repr(e.lineno)


# --- TypeVar bound / constraint / default across every carrier ---

expect_syntax_error("def f[T: (yield 1)](): pass", "yield expression cannot be used within a TypeVar bound")
expect_syntax_error("def f[T: (x := 1)](): pass", "named expression cannot be used within a TypeVar bound")
expect_syntax_error("async def f[T: (await x)](): pass", "await expression cannot be used within a TypeVar bound")
expect_syntax_error("class C[T = (yield 1)]: pass", "yield expression cannot be used within a TypeVar default")
expect_syntax_error("class C[T = (await x)]: pass", "await expression cannot be used within a TypeVar default")
expect_syntax_error("def f[T = (x := 1)](): pass", "named expression cannot be used within a TypeVar default")
expect_syntax_error("def f[*Ts = (yield 1)](): pass", "yield expression cannot be used within a TypeVarTuple default")
expect_syntax_error("def f[**P = (yield 1)](): pass", "yield expression cannot be used within a ParamSpec default")
expect_syntax_error("type Alias[T: (yield)] = int", "yield expression cannot be used within a TypeVar bound")

# a tuple bound reads as a constraint wherever the walrus hides in it
expect_syntax_error("def f[T: (int, (y := 2))](): pass", "named expression cannot be used within a TypeVar constraint")
expect_syntax_error("def f[T: ((yield 1), int)](): pass", "yield expression cannot be used within a TypeVar constraint")

# --- type alias values ---

expect_syntax_error("type A = (yield)", "yield expression cannot be used within a type alias")
expect_syntax_error("type A = (x := 1)", "named expression cannot be used within a type alias")

# --- annotations: statement targets, class bodies, signatures ---

expect_syntax_error("x: (yield) = 1", "yield expression cannot be used within an annotation")
expect_syntax_error("x: (y := 1) = 1", "named expression cannot be used within an annotation")
expect_syntax_error("def g(a: (yield)): pass", "yield expression cannot be used within an annotation")
expect_syntax_error("class C: x: (yield)", "yield expression cannot be used within an annotation")
expect_syntax_error("def g(): x: (y := 1) = 1", "named expression cannot be used within an annotation")
expect_syntax_error("def g() -> (yield): pass", "yield expression cannot be used within an annotation")
expect_syntax_error("def g(*args: (yield)): pass", "yield expression cannot be used within an annotation")
expect_syntax_error("def g(**kw: (x := 1)): pass", "named expression cannot be used within an annotation")
expect_syntax_error("async def g(a: (await x)): pass", "await expression cannot be used within an annotation")
expect_syntax_error("def g(a, /, b: (yield)): pass", "yield expression cannot be used within an annotation")
expect_syntax_error("def g(*, b: (yield)): pass", "yield expression cannot be used within an annotation")

# --- the restriction reaches through nested call/attribute positions ---

expect_syntax_error("type A = f((yield))", "yield expression cannot be used within a type alias")
expect_syntax_error("type A = obj.attr[(x := 1)]", "named expression cannot be used within a type alias")
expect_syntax_error("def f[T: [1][(yield 1)]](): pass", "yield expression cannot be used within a TypeVar bound")

# --- nested scopes lift the restriction (CPython block semantics) ---

# a lambda's defaults stay in the restricted block, its body does not
expect_syntax_error("type A = (lambda x=(yield 1): x)", "yield expression cannot be used within a type alias")

compile("type A = (lambda: (yield))", "<test>", "exec")
compile("def f[T: (lambda: (yield))](): pass", "<test>", "exec")
compile("type A = [x for x in (yield)]".replace("in (yield)", "in (1,)"), "<test>", "exec")

# annotations and bounds that use none of the three kinds still compile
compile("def f[T: int](): pass", "<test>", "exec")
compile("type A[T] = list[T]", "<test>", "exec")
compile("x: list[int] = []", "<test>", "exec")

print("test_restricted_expr_blocks passed")
