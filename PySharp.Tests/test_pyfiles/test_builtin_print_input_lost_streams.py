"""Verifies print() and input() resolve sys.std* at call time, distinguish a missing stream from one bound to None, and validate their arguments in CPython's order. Documented deviation: none.

:kind: test
:background: CPython's builtin_print_impl (Python/bltinmodule.c) fetches sys.stdout through _PySys_GetRequiredAttr, which raises RuntimeError("lost sys.stdout") when the attribute is absent, while a None value means "not connected" and makes print do nothing. builtin_input_impl instead requires all three of stdin/stdout/stderr before doing any work, treating None like a missing one. The argument clinic wrapper also converts flush before the implementation runs, so a failing __bool__ outranks every other check, and the implementation resolves sys.stdout ahead of validating sep/end.
"""

import sys


class BadBool:
    def __bool__(self):
        raise ValueError("badbool")


class BadStr:
    def __str__(self):
        raise ValueError("badstr")


def capture(fn):
    try:
        fn()
        return "NO ERROR"
    except BaseException as e:
        return type(e).__name__ + ": " + str(e)


saved = (sys.stdin, sys.stdout, sys.stderr)


def restore():
    sys.stdin, sys.stdout, sys.stderr = saved


results = []

# === print(): a missing sys.stdout is an error; one bound to None is silent ===
del sys.stdout
results.append(capture(lambda: print("x")))
results.append(capture(lambda: print(1, sep=2)))
results.append(capture(lambda: print(1, end=2)))
restore()

sys.stdout = None
results.append(capture(lambda: print("x")))
# None short-circuits before the values are stringified or sep/end validated
results.append(capture(lambda: print(1, sep=2)))
results.append(capture(lambda: print(BadStr())))
restore()

# === print(): flush is converted first, so its failure outranks everything ===
results.append(capture(lambda: print(1, flush=BadBool())))
results.append(capture(lambda: print(1, sep=2, flush=BadBool())))
del sys.stdout
results.append(capture(lambda: print(1, flush=BadBool())))
restore()
# the flush conversion also outranks stringifying an argument
results.append(capture(lambda: print(BadStr(), flush=BadBool())))
with open("print_input_probe.txt", "w") as f:
    results.append(capture(lambda: print(BadStr(), file=f, flush=BadBool())))
results.append(capture(lambda: print(BadStr())))

# === print(): sep/end are validated after sys.stdout resolves, and normally ===
results.append(capture(lambda: print(1, sep=2)))
results.append(capture(lambda: print(1, end=2)))
# sep/end of None mean the defaults, and the write goes to a file so this
# fixture's own stdout stays free of incidental output
with open("print_input_probe.txt", "w") as f:
    results.append(capture(lambda: print(1, sep=None, end=None, file=f)))

# === print(file=...): an explicit file bypasses sys.stdout entirely ===
del sys.stdout
with open("print_input_probe.txt", "w") as f:
    results.append(capture(lambda: print("x", file=f)))
results.append(capture(lambda: print("x", file=None)))
restore()

# === input(): all three streams are required, and a None one is an error ===
del sys.stdin
results.append(capture(lambda: input()))
restore()
sys.stdin = None
results.append(capture(lambda: input()))
restore()

del sys.stdout
results.append(capture(lambda: input()))
restore()
sys.stdout = None
results.append(capture(lambda: input()))
restore()

del sys.stderr
results.append(capture(lambda: input()))
restore()
sys.stderr = None
results.append(capture(lambda: input()))
restore()

# precedence: stdin is checked before stdout, which is checked before stderr.
# Each case leaves exactly one more stream intact than the one before, so the
# reported name is the first stream found missing in the check order.
del sys.stdin
del sys.stdout
del sys.stderr
results.append(capture(lambda: input()))
sys.stdin = saved[0]
results.append(capture(lambda: input()))
restore()
del sys.stderr
results.append(capture(lambda: input()))
restore()

# === input(): every stream present, stdin at EOF ===
results.append(capture(lambda: input()))

expected = [
    # print with a missing / None sys.stdout
    "RuntimeError: lost sys.stdout",
    "RuntimeError: lost sys.stdout",
    "RuntimeError: lost sys.stdout",
    "NO ERROR",
    "NO ERROR",
    "NO ERROR",
    # flush converted first
    "ValueError: badbool",
    "ValueError: badbool",
    "ValueError: badbool",
    "ValueError: badbool",
    "ValueError: badbool",
    "ValueError: badstr",
    # sep/end validated normally
    "TypeError: sep must be None or a string, not int",
    "TypeError: end must be None or a string, not int",
    "NO ERROR",
    # an explicit file bypasses sys.stdout
    "NO ERROR",
    "RuntimeError: lost sys.stdout",
    # input() requires all three streams
    "RuntimeError: lost sys.stdin",
    "RuntimeError: lost sys.stdin",
    "RuntimeError: lost sys.stdout",
    "RuntimeError: lost sys.stdout",
    "RuntimeError: lost sys.stderr",
    "RuntimeError: lost sys.stderr",
    # stdin outranks stdout, which outranks stderr
    "RuntimeError: lost sys.stdin",
    "RuntimeError: lost sys.stdout",
    "RuntimeError: lost sys.stderr",
    # all intact, at EOF
    "EOFError: EOF when reading a line",
]
assert results == expected, "\n".join(
    f"{i}: got {got!r}, want {want!r}" for i, (got, want) in enumerate(zip(results, expected)) if got != want
)

assert sys.stdin is saved[0] and sys.stdout is saved[1] and sys.stderr is saved[2]
print("print/input lost-stream semantics ok")
