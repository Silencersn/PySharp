"""CRLF sources produce universal-newline SyntaxError.text (no stray CR).

:kind: test
"""

# Regression: the line handed to SyntaxError.text kept the physical CRLF
# ending, so the rendered source line emitted a doubled \r\r\n and
# e.text read '...\r\n' instead of '...\n'. The tokenizer-side
# normalization of CPython translates both files to the same text.


def build(path, ending):
    with open(path, "wb") as f:
        f.write(b"x = = 1" + ending)
    return path


def run_compile(path):
    try:
        compile(open(path, "rb").read(), path, "exec")
        return "no-error", ""
    except SyntaxError as e:
        return "SyntaxError", repr(e.text)


print("crlf:", *run_compile(build("crlf_syntax.py", b"\r\n")))
print("lf:", *run_compile(build("lf_syntax.py", b"\n")))

with open("crlf_open.py", "wb") as f:
    f.write(b"def f(\r\n")
try:
    exec(compile(open("crlf_open.py", "rb").read(), "crlf_open.py", "exec"))
except SyntaxError as e:
    print("open:", repr(e.text))

with open("crlf_runtime.py", "wb") as f:
    f.write(b"1 / 0\r\n")
try:
    exec(compile(open("crlf_runtime.py", "rb").read(), "crlf_runtime.py", "exec"))
except ZeroDivisionError:
    print("runtime: ZeroDivisionError")
