"""
Bitwise inversion of a bool emits the CPython 3.14
DeprecationWarning on every form that reaches the slot - a variable
holding a bool, a `True`/`False` literal, and the explicit
`__invert__()` call - while the value stays the int `~int(x)` result.
A non-bool operand (int) reuses the int slot and stays silent.

CPython 3.14 reference (Objects/boolobject.c bool_invert): warn via
PyErr_WarnEx(PyExc_DeprecationWarning) before delegating to the int
nb_invert; removal is scheduled for Python 3.16. Python/flowgraph.c
eval_const_unaryop deliberately refuses to constant-fold
UNARY_INVERT on a bool, so the warning is issued at runtime and is
reachable by the warning filters - it can be recorded and escalated.

The module-level forms below warn through the default filter (the C#
side, TestBoolInvertDeprecationWarning, captures stderr and asserts
the message); the assertions then pin the record, escalation and
per-location dedup semantics.

:kind: test
"""

import warnings

# module-level triggers: these reach stderr under the default filter
t, f = True, False
literal_result = ~True
variable_result = ~t
method_result = t.__invert__()

MESSAGE = (
    "Bitwise inversion '~' on bool is deprecated and will be removed in "
    "Python 3.16. This returns the bitwise inversion of the underlying int "
    "object and is usually not what you expect from negating a bool. Use the "
    "'not' operator for boolean negation or ~int(x) if you really want the "
    "bitwise inversion of the underlying int."
)


def caught(code):
    with warnings.catch_warnings(record=True) as w:
        warnings.simplefilter("always")
        value = eval(code)
        return value, [(item.category.__name__, str(item.message)) for item in w]


# every form that reaches the bool slot warns once, with the int result
for expression in ("~t", "~True", "~False", "t.__invert__()", "f.__invert__()"):
    value, warned = caught(expression)
    assert warned == [("DeprecationWarning", MESSAGE)], (expression, warned)
    assert type(value) is int, (expression, type(value))

# the values are the bitwise inversion of the underlying int
assert caught("~t")[0] == -2
assert caught("~f")[0] == -1

# a non-bool operand reuses the int slot and stays silent
assert caught("~1") == (-2, []), caught("~1")
assert caught("~0") == (-1, []), caught("~0")

# a bool produced by an expression is still a bool operand
value, warned = caught("~(1 == 1)")
assert value == -2 and len(warned) == 1, (value, warned)

# the nested form warns only for the bool operand, not for the int result
value, warned = caught("~~t")
assert value == 1 and len(warned) == 1, (value, warned)

# a runtime warning is reachable by the filters: escalating it raises
try:
    with warnings.catch_warnings():
        warnings.simplefilter("error", DeprecationWarning)
        ~t
except DeprecationWarning as exc:
    assert str(exc) == MESSAGE, str(exc)
else:
    raise AssertionError("~bool must escalate to an error under simplefilter('error')")

# the default filter shows it once per location, not once per execution
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("default")
    for _ in range(3):
        ~t
    assert len(w) == 1, [str(item.message) for item in w]

# the three module-level forms above kept the int result
assert (literal_result, variable_result, method_result) == (-2, -2, -2), (
    literal_result, variable_result, method_result)

print("test_bool_invert_deprecation_warning passed")
