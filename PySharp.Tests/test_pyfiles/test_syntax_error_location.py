"""Verifies that parse-time SyntaxErrors carry the full CPython location tuple (filename, lineno, offset, text, end_lineno, end_offset, msg and info args), while runtime-raised SyntaxErrors have no location info and the two-argument constructor path is unchanged.

:kind: test
"""

# SyntaxError location attributes. CPython raises parse-time errors with
# the full location info tuple (_PyPegen_raise_error_known_location), so
# the display layer renders the File/caret block from the exception's own
# attributes instead of a traceback frame, and the "Traceback (most recent
# call last):" header only appears for exceptions that carry real frames.
def attrs(e):
    return (e.filename, e.lineno, e.offset, e.text, e.end_lineno, e.end_offset)

# --- compile() errors carry the full location tuple ---

try:
    compile("def broken(:", "x.py", "exec")
    raise AssertionError("expected SyntaxError")
except SyntaxError as e:
    assert type(e) is SyntaxError
    filename, lineno, offset, text, end_lineno, end_offset = attrs(e)
    assert filename == "x.py"
    assert lineno == 1
    assert isinstance(offset, int) and offset >= 1
    assert text.rstrip("\n") == "def broken(:"
    assert isinstance(end_lineno, int) and end_lineno == lineno
    assert isinstance(end_offset, int)
    assert e.msg and isinstance(e.msg, str)
    # CPython parse errors carry (msg, info-tuple) args
    assert len(e.args) == 2 and e.args[0] == e.msg
    assert e.args[1] == (filename, lineno, offset, text, end_lineno, end_offset)

try:
    compile("x = 1\n  y = 2\n", "t.py", "exec")
    raise AssertionError("expected IndentationError")
except IndentationError as e:
    assert isinstance(e, SyntaxError)
    filename, lineno, offset, text, end_lineno, end_offset = attrs(e)
    assert filename == "t.py"
    assert lineno == 2
    assert text == "  y = 2\n"
    assert len(e.args) == 2 and e.args[0] == e.msg

# --- runtime-raised SyntaxError has no location info ---

def boom():
    raise SyntaxError("boom")

try:
    boom()
    raise AssertionError("expected SyntaxError")
except SyntaxError as e:
    assert e.filename is None and e.lineno is None and e.offset is None
    assert e.text is None and e.end_lineno is None and e.end_offset is None
    assert str(e) == "boom"

# --- the two-argument info-tuple constructor path is unchanged ---

e = SyntaxError("m", ("f.py", 3, 5, "hello\n", 3, 7))
assert attrs(e) == ("f.py", 3, 5, "hello\n", 3, 7)
assert str(e) == "m (f.py, line 3)"

e = SyntaxError("only")
assert (e.filename, e.lineno, e.text) == (None, None, None)
assert str(e) == "only"
