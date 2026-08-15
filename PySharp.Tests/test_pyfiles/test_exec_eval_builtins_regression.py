"""
Regression: exec()/eval() with an explicit globals dict must inject the
interpreter's builtins when the dict lacks a __builtins__ key (CPython
behavior), instead of raising NameError for builtin names.

CPython 3.14 reference:
    g = {}
    exec('z = 7', g)                  # works; g['z'] == 7, '__builtins__' in g
    eval('len([1, 2, 3])', {}) == 3
    eval('abs(-5)')                   # globals=None uses current frame builtins
    exec('print(1)', {'__builtins__': None})   # user value respected -> NameError
"""

# 1. Explicit empty globals: builtins injected automatically.
g = {}
exec('z = 7', g)
assert g['z'] == 7
assert '__builtins__' in g

assert eval('len([1, 2, 3])', {}) == 3
exec('print(123)', {})

# 2. globals=None path (current frame's builtins) still works.
assert eval('abs(-5)') == 5
exec('assert all([1, 2, 3])')  # all() is a builtin

# 3. User-provided __builtins__ is respected (not overwritten).
try:
    exec('print(1)', {'__builtins__': None})
    assert False, "exec with __builtins__=None should raise NameError"
except NameError:
    pass

print("test_exec_eval_builtins_regression passed")
