"""
Regression: a form feed (U+000C) inside indentation must reset the column
counter to zero, like CPython (Parser/lexer/lexer.c:529, "For Emacs
users": col = altcol = 0). PySharp used to treat it as ordinary
indentation whitespace, which breaks both directions:

- legal source rejected:  "\x0cx = 1" at line start raised
  IndentationError: unexpected indent (CPython: fine, x is at column 0);
- illegal source accepted: "if True:\n    \x0cpass" compiled fine
  (CPython: IndentationError, pass lands at column 0 inside the block).

Forms where PySharp already matches CPython are pinned as guards: FF
alone on a line, FF in the middle of a line (plain whitespace), and FF
followed by spaces as a valid block indent.
"""

def compiles(src):
    try:
        compile(src, "<test>", "exec")
        return True
    except Exception:
        return False


# red case 1: FF at line start with code right after (legal)
ns = {}
ff_line_start_ok = True
try:
    exec("\x0cx = 1\ny = 2\n", ns)
except Exception:
    ff_line_start_ok = False
assert ff_line_start_ok, "FF at line start must reset the column, not indent"
assert ns.get("x") == 1 and ns.get("y") == 2


# red case 2: several leading FFs behave the same way
assert compiles("\x0c\x0c\x0cz = 5\n"), "leading FFs must reset the column"


# red case 3: spaces, then FF, then code inside a block (illegal)
assert not compiles("if True:\n    \x0cpass\nprint('ok')\n"), (
    "space+FF+code inside a block must be an IndentationError")


# --- guards: FF positions already handled correctly ---

ns = {}
exec("a = 1\n\x0c\nb = 2\n", ns)
assert ns["a"] == 1 and ns["b"] == 2        # FF alone on a line: ignored

ns = {}
exec("c = 1 +\x0c2\n", ns)
assert ns["c"] == 3                          # FF mid-line: plain whitespace

assert compiles("if True:\n\x0c    pass\n")  # FF + spaces: valid block indent

print("test_form_feed_indent_regression passed")
