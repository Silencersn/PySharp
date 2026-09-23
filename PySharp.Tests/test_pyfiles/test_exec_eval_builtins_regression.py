"""
Regression: exec()/eval() with an explicit globals dict must inject the
interpreter's builtins when the dict lacks a __builtins__ key (CPython
behavior), instead of raising NameError for builtin names.

CPython 3.14 reference:
    g = {}
    exec('z = 7', g)                  # works; g['z'] == 7, '__builtins__' in g
    eval('len([1, 2, 3])', {}) == 3
    eval('abs(-5)')                   # globals=None uses current frame builtins
    exec('print(1)', {'__builtins__': None})   # TypeError, lazily at lookup
    exec('print(9)', {'__builtins__': {'print': print}})  # user mapping used as-is
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
# CPython: the value becomes the frame's builtins mapping as-is. None is not
# subscriptable, so the failure surfaces lazily — only when a name lookup
# actually falls through to builtins — as TypeError, not NameError
# (Python/ceval.c: _PyEval_LoadName / _PyEval_LoadGlobalStackRef).
try:
    exec('print(1)', {'__builtins__': None})
    assert False, "exec with __builtins__=None should raise TypeError"
except TypeError as e:
    assert str(e) == "'NoneType' object is not subscriptable", e

# No builtin-name lookup -> no error at all (lazy failure, like CPython).
exec('pass', {'__builtins__': None})

# 4. A user mapping (non-module) is used as the builtins mapping directly.
exec('print(42)', {'__builtins__': {'print': print}})

d = {'value': 7}
assert eval('value * 6', {'__builtins__': d}) == 42

# 5. The same rules apply to the function-body LOAD_GLOBAL path.
try:
    exec('def f(): return print(1)\nf()', {'__builtins__': None})
    assert False, "function-body lookup with __builtins__=None should raise TypeError"
except TypeError as e:
    assert str(e) == "'NoneType' object is not subscriptable", e

print("test_exec_eval_builtins_regression passed")
