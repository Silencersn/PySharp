# test_async_comp_errors: Verify that async comprehension outside async function fails
# Uses compile() to test at compile time without executing

import traceback

_errors = 0

def check_error(description, source):
    global _errors
    try:
        compile(source, "<test>", "exec")
        print(f"UNEXPECTED: {description} - no error")
    except SyntaxError as e:
        print(f"OK: {description} - {e.msg}")
        _errors += 1

# Error: async for in list comprehension in sync function
check_error(
    "async for in list comp in sync function",
    "def f():\n    result = [x async for x in range(3)]\n"
)

# Error: async for in set comprehension in sync function
check_error(
    "async for in set comp in sync function",
    "def f():\n    result = {x async for x in range(3)}\n"
)

# Error: async for in dict comprehension in sync function
check_error(
    "async for in dict comp in sync function",
    "def f():\n    result = {k: k async for k in range(3)}\n"
)

# Error: async for in list comprehension at module level
check_error(
    "async for in list comp at module level",
    "result = [x async for x in range(3)]\n"
)

# Error: async for in list comprehension in class body
check_error(
    "async for in list comp in class body",
    "class Foo:\n    result = [x async for x in range(3)]\n"
)

# Valid: async for in genexp (lazy) — should NOT error even in sync function
try:
    code = compile("gen = (x async for x in range(3))", "<test>", "exec")
    ns = {}
    exec(code, ns)
    print(f"OK: async for in genexp in sync function compiles and creates {type(ns['gen']).__name__}")
    _errors += 1
except SyntaxError as e:
    print(f"UNEXPECTED: async for in genexp - {e.msg}")

# Summary
assert _errors == 5, f"Expected 5 errors, got {_errors}"
print(f"\nAll {_errors} error tests passed")

