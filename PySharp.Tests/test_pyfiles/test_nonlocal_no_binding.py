"""unbound nonlocal keeps its declaration's location through the closure pass.

:kind: test
"""

# Regression: the deferred closure check ran after the tree walk, when the
# node stack was empty, so the SyntaxError carried no file, line, source
# text or caret at all. The declaring NonlocalNode is now recorded per
# scope, and its statement-sized range gives the whole-statement caret
# ('nonlocal x' = ten carets), like CPython's symtable_error.

CASES = [
    ("nested", b"def f():\n    def g():\n        nonlocal x\n        x = 1\n    g()\nf()\n"),
    ("module", b"nonlocal x\n"),
    ("two-names", b"def f():\n    def g():\n        nonlocal x, y\n        x = 1\n    g()\nf()\n"),
    ("ok", b"def f():\n    x = 1\n    def g():\n        nonlocal x\n        x = 2\n    g()\n    return x\nprint(f())\n"),
]

for name, content in CASES:
    try:
        code = compile(content, name, "exec")
        exec(code, {"print": print})
        print(name, "ran")
    except SyntaxError as e:
        print(name, "|", e.msg, "|", e.lineno, e.offset, "|", repr(e.text))
