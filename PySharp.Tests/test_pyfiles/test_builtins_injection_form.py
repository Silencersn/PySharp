"""
__builtins__ must be injected in the form CPython picks per
context, instead of always being the builtins module.

CPython 3.14 reference (Python/pylifecycle.c add_main_module,
Python/import.c module_dict_for_exec, Python/bltinmodule.c exec/eval ->
Python/ceval.c _PyEval_EnsureBuiltins):

    # __main__ holds the builtins module itself
    __builtins__ is builtins                            # True

    # exec()/eval() globals hold builtins.__dict__ — that very dict, not a copy
    ns = {}; exec('pass', ns)
    ns['__builtins__'] is builtins.__dict__             # True

    # an imported module gets the same dict
    import json
    json.__dict__['__builtins__'] is builtins.__dict__  # True

The dict form is what lets the entry be read as a mapping, which is how
sandboxes and dynamic-execution code normally consume it.

:kind: test
"""

import builtins
import test_imported

# 1. __main__ keeps the builtins module (pylifecycle.c add_main_module).
assert type(__builtins__).__name__ == "module"
assert __builtins__ is builtins

# 2. exec()/eval() globals get builtins.__dict__ itself.
ns = {}
exec('a = 1', ns)
assert ns['a'] == 1
assert type(ns['__builtins__']) is dict
assert ns['__builtins__'] is builtins.__dict__

ns2 = {}
assert eval('len([1, 2, 3])', ns2) == 3
assert type(ns2['__builtins__']) is dict
assert ns2['__builtins__'] is builtins.__dict__

# 3. An imported module gets the same dict.
mod_builtins = test_imported.__dict__['__builtins__']
assert type(mod_builtins) is dict
assert mod_builtins is builtins.__dict__

# 4. The entry is a real mapping: the reads that used to fail now work.
assert ns['__builtins__']['len'] is len
assert ns['__builtins__'].get('len') is len
assert mod_builtins['len'] is len
assert 'len' in ns['__builtins__']

# 5. Function and class-body frames inherit the enclosing module's builtins.
def f():
    g = {}
    exec('pass', g)
    return g['__builtins__']

assert f() is builtins.__dict__

class C:
    ns = {}
    exec('pass', ns)

assert C.ns['__builtins__'] is builtins.__dict__

# 6. The running frame's own mapping is what gets propagated: a custom
#    mapping travels as-is, and None travels as None (ceval.c
#    _PyEval_EnsureBuiltins -> PyEval_GetBuiltins).
custom = {'marker': 1, 'exec': exec}
out = {}
exec("exec('pass', out)", {'__builtins__': custom, 'exec': exec, 'out': out})
assert out['__builtins__'] is custom

none_out = {}
exec("exec('pass', none_out)", {'__builtins__': None, 'exec': exec, 'none_out': none_out})
assert none_out['__builtins__'] is None

# 7. A user-provided __builtins__ is still never overwritten.
kept = {'__builtins__': custom}
exec('pass', kept)
assert kept['__builtins__'] is custom

print("test_builtins_injection_form passed")
