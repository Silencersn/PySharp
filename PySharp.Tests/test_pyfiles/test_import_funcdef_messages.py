"""import and function-definition productions name their failures.

:kind: test
"""

# Battery over CPython's import/funcdef recovery sentences: the py2-style
# "Did you mean to use 'from ... import ...' instead?" hint, expected '('
# after the function name, the unparenthesized trailing comma, the
# literal/attribute import targets, the empty default value and the
# parenthesized parameter list — with the previously-agreeing fragments
# kept as a baseline.

snippets = [
    "import a from b", "import a.y.z from b", "import a from b as bar",
    "import a, b,c from b", "def f:", "async def f:", "def f -> int:",
    "from math import pi,", "import math as 5", "from math import pi as 5",
    "import a.b.c as d.e", "def foo(a=1, d=, c): pass",
    "def f(x, (y, z), w): pass", "def f((x, y)): pass",
    "class C:", "from math import (pi,)",
    "def foo(a, *a, b, **c, d): pass", "import math", "from math import pi",
]
for src in snippets:
    try:
        compile(src, "<s>", "exec")
        print(repr(src), "-> OK")
    except SyntaxError as e:
        print(repr(src), "->", e.msg, "| line", e.lineno)
