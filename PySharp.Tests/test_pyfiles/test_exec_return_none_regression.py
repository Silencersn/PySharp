r"""
Regression: exec() must always return None, like CPython's
builtin_exec_impl, which evaluates the program and drops its result:

    result = PyEval_EvalCode((PyObject *)prog, globals, locals);
    if (result == NULL) return NULL;
    Py_DECREF(result);
    Py_RETURN_NONE;

This implementation returned the value of the last expression whenever
it was handed a code object compiled in 'eval' mode, so
`exec(compile('1', '<test>', 'eval'))` produced 1 instead of None, and a
REPL echoed the value.

Guards: eval() still returns the value (from source strings and from
'eval'-mode code objects), and exec() still applies its side effects to
the target namespace.
"""

# red case: an 'eval'-mode code object must still yield None from exec()
assert exec(compile("1", "<test>", "eval")) is None

# string sources are compiled as 'exec' and already returned None; pin them
assert exec("1") is None
assert exec("zz = 9") is None

# an 'exec'-mode code object holding an expression statement
assert exec(compile("1\n", "<test>", "exec")) is None

# guards: eval keeps returning the value
assert eval("1") == 1
assert eval(compile("2", "<test>", "eval")) == 2

# guards: side effects still land in the provided namespace
ns = {}
exec("y = 5", ns)
assert ns["y"] == 5

ns2 = {}
exec(compile("z = 6\n", "<test>", "exec"), ns2)
assert ns2["z"] == 6

print("test_exec_return_none_regression passed")
