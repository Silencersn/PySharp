"""loop-control placement errors keep CPython's two distinct sentences.

:kind: test
"""

# Regression: 'continue' outside a loop said "outside loop". CPython's
# codegen_continue keeps its historical sentence "'continue' not properly
# in loop" (it once had to single out continue-in-finally), while break
# genuinely reports "'break' outside loop".

CASES = [
    ("continue", b"continue\n"),
    ("break", b"break\n"),
    ("loop-continue", b"for i in []:\n    continue\n"),
    ("loop-break", b"while True:\n    break\n"),
]

for name, content in CASES:
    try:
        compile(content, name, "exec")
        print(name, "compiled")
    except SyntaxError as e:
        print(name, "|", e.msg, "|", e.lineno, e.offset)
