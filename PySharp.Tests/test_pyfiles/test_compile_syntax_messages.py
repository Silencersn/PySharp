"""compile()-stage syntax messages match CPython's seven divergences.

:kind: test
"""

# Battery over the assignment-target, del, star-argument, module await,
# match-block and lambda-body messages, with the previously-agreeing
# fragments kept as a regression baseline. parser.c's invalid-target rule
# carries the "Maybe you meant '=='" hint, while ast.c's reserved-name
# check and chained-assignment targets keep the bare sentence;
# symtable.c picks "'await' outside function" above function scope.

frags = ["1 = 2", "f() = 1", "[x for x in y] = 1", "del 1",
         "def f(*a, *b): pass", "await x", "match x:", "lambda a=1, b",
         "def f(x, x)", "lambda x, x", "None = 1", "True = 1", "(1 := 2)",
         "x++", "class = 1", "import = 1", "def f(**k, **k2)", "def f(a=1, b)",
         "lambda a=1, b: 0", "global __debug__", "x = *1", "def f(): return, 1",
         "case 1, 2:", "x = 1; x = 2 = 3", "for 1 in y",
         "def f(a, /, /): pass", "async def h(): await x", "def s(): await x",
         "class C: await x", "def f(a=1, b)", "del f()", "del f()"]
for src in frags:
    try:
        compile(src, "<s>", "exec")
        print(repr(src), "-> ok")
    except SyntaxError as e:
        print(repr(src), "->", e.msg)
