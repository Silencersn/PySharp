"""dedicated sentences replace the invalid-syntax fallback (13 sites).

:kind: test
"""

# Battery over CPython's PEG error-recovery sentences: the unparenthesized
# generator expression, a bare not after an operator, a starred dictionary
# value, a missing else, an empty import, a bare star in a call, unpacking
# inside a comprehension, the empty PEP 695 list, lambda's parenthesized
# parameters, the incompatible u prefix, mixed match-class patterns and
# the missing keyword-argument value — with the previously-agreeing
# fragments kept as a baseline. (def f(: pass is kept out: PySharp fails
# earlier with "'(' was never closed", a deeper failure-point question.)

snippets = [
    "sum(x for x in y, 1)", "1 + not 2", "{1: *x}", "x = 1 if 2",
    "import", "print(*)", "f(*x for x in y)", "class C[]: pass",
    "lambda (x): 1", 'ur"x"', "match p:\n    case P(x=1, 2): pass",
    "f(a=)", "f(a, b, c=, d)", "def f(a=): pass", "from math import 5",
]
for src in snippets:
    try:
        compile(src, "<s>", "exec")
        print(repr(src), "-> OK")
    except SyntaxError as e:
        print(repr(src), "->", e.msg, "| line", e.lineno)


def u(s): return s
print("u-call =", repr(u("a")))
try:
    compile('ur"x"', "<s>", "exec")
except SyntaxError as e:
    print("ur still !", e.msg)
