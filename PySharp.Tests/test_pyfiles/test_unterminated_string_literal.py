"""unterminated string literals report their own sentence and line.

:kind: test
"""

# Regression: single/double quotes fell through to the generic "invalid
# syntax" and a triple-quoted literal reported its opening line. CPython's
# tokenizer.c raises the dedicated sentence with the line the scanner
# reached (tok->lineno: the last physical line for a triple-quoted
# literal, the opening line itself for a single-line one), while the
# caret and e.lineno stay on the opening line.

CASES = [
    ("sq-eof", b"x = 'a"),
    ("sq-newline", b"x = 'a\nb'"),
    ("dq-eof", b'x = "a'),
    ("sq-eol", b"x = 'a\n"),
    ("tri-two", b"s = '''abc\nmore\n"),
    ("tri-three", b"s = '''abc\nmore\ntext\n"),
    ("tri-indented", b"\n\n\ns = '''abc\nmore\n"),
    ("tri-eof", b"s = '''abc"),
    ("dq3", b's = """abc\nmore\n'),
]

for name, content in CASES:
    try:
        compile(content, name, "exec")
        print(name, "compiled")
    except SyntaxError as e:
        print(name, "|", e.msg, "|", e.lineno, e.offset, "|", repr(e.text))
