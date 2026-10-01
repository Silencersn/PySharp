"""Error-message conformance for C-implemented builtins and method descriptors.

CPython 3.14 reference: Objects/methodobject.c (cfunction_vectorcall_NOARGS/_O,
cfunction_check_kwargs), Objects/descrobject.c (method_check_args, descr_check),
Python/getargs.c (_PyArg_CheckPositional and the clinic parser families) and
_Py_Check_ArgsIterable/LIST_EXTEND (Python/bytecodes.c) for the *-expansion
messages. A callable is named by _PyObject_FunctionStr: __qualname__ with a
non-builtin __module__ prefix.

:kind: test
"""


def drive(call):
    try:
        call()
    except TypeError as e:
        return str(e)
    return "no error"


def f(*args, **kwargs):
    return args


class D:
    def __call__(self, **k):
        return k


results = [
    # vectorcall families: full _PyObject_FunctionStr name
    drive(lambda: len()),
    drive(lambda: len(1, 2)),
    drive(lambda: len(x=1)),
    drive(lambda: [].append()),
    drive(lambda: [].append(1, 2)),
    drive(lambda: [].append(x=1)),
    drive(lambda: {}.clear(1)),
    drive(lambda: "".join()),
    drive(lambda: "".join("a", "b")),
    # method descriptors: unbound needs-an-argument and descr_check
    drive(lambda: list.append()),
    drive(lambda: str.upper(1)),
    drive(lambda: list.append(1, 2)),
    # _PyArg_CheckPositional family: bare name, no parentheses
    drive(lambda: {}.get()),
    drive(lambda: "a".split("b", "c", "d")),
    # clinic parser family: bare name with parentheses
    drive(lambda: sum()),
    drive(lambda: round()),
    drive(lambda: round(1, 2, 3)),
    drive(lambda: pow(1)),
    drive(lambda: open()),
    # constructor families
    drive(lambda: bool(1, 2)),
    drive(lambda: float(1, 2)),
    drive(lambda: float(1, 2, 3)),
    drive(lambda: float(x=1)),
    drive(lambda: int(1, 2, 3)),
    drive(lambda: int(x=5)),
    # * expansion: the lone starred call names the callable
    drive(lambda: f(*1)),
    drive(lambda: f(*1.5)),
    drive(lambda: f(*None)),
    drive(lambda: f(*True)),
    drive(lambda: len(*1)),
    # * expansion merged into a list: no callable to name
    drive(lambda: f(*"ab", *1)),
    drive(lambda: [1, *1]),
]

expected = [
    "len() takes exactly one argument (0 given)",
    "len() takes exactly one argument (2 given)",
    "len() takes no keyword arguments",
    "list.append() takes exactly one argument (0 given)",
    "list.append() takes exactly one argument (2 given)",
    "list.append() takes no keyword arguments",
    "dict.clear() takes no arguments (1 given)",
    "str.join() takes exactly one argument (0 given)",
    "str.join() takes exactly one argument (2 given)",
    "unbound method list.append() needs an argument",
    "descriptor 'upper' for 'str' objects doesn't apply to a 'int' object",
    "descriptor 'append' for 'list' objects doesn't apply to a 'int' object",
    "get expected at least 1 argument, got 0",
    "split() takes at most 2 arguments (3 given)",
    "sum() takes at least 1 positional argument (0 given)",
    "round() missing required argument 'number' (pos 1)",
    "round() takes at most 2 arguments (3 given)",
    "pow() missing required argument 'exp' (pos 2)",
    "open() missing required argument 'file' (pos 1)",
    "bool expected at most 1 argument, got 2",
    "float expected at most 1 argument, got 2",
    "float expected at most 1 argument, got 3",
    "float() takes no keyword arguments",
    "int expected at most 2 arguments, got 3",
    "int() got an unexpected keyword argument 'x'",
    "__main__.f() argument after * must be an iterable, not int",
    "__main__.f() argument after * must be an iterable, not float",
    "__main__.f() argument after * must be an iterable, not NoneType",
    "__main__.f() argument after * must be an iterable, not bool",
    "len() argument after * must be an iterable, not int",
    "Value after * must be an iterable, not int",
    "Value after * must be an iterable, not int",
]

for got, want in zip(results, expected):
    assert got == want, f"\n  got:  {got}\n  want: {want}"

# the callable's own __qualname__/__module__ carry into the **-expansion
# message (attribute lookup, not a type switch)
d = D()
d.__qualname__ = "q"
d.__module__ = "m"
try:
    d(b=2, **{"b": 1})
    assert False, "expected TypeError"
except TypeError as e:
    assert str(e) == "m.q() got multiple values for keyword argument 'b'", str(e)

# reverse controls: well-formed calls keep working
assert f(*[1]) == (1,)
assert f(**{}) == ()
assert round(1, ndigits=1) == 1.0
assert int("10", base=16) == 16
assert len([1, 2]) == 2

try:
    len(object=1)
    assert False, "expected TypeError"
except TypeError as e:
    assert str(e) == "len() takes no keyword arguments", str(e)

print("test_builtin_call_error_messages passed")
