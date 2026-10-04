"""Constant callee/subscript warns at compile time about a missed comma.

CPython's codegen checks three shapes that almost always mean a missing
comma in a tuple/display (codegen.c check_caller / check_subscripter /
check_index): calling a literal or collection display, subscripting a
constant that is never subscriptable (None/Ellipsis/int/float/complex)
or a set/generator/lambda/t-string display, and subscripting a str/
bytes/tuple constant or tuple/list/str display with an operand of a
known non-index type. Each warns once per site through the SyntaxWarning
channel before the runtime TypeError.

An escalated warning (warnings filter "error") is reported as a
SyntaxError carrying the compile location (errors.c
_PyErr_EmitSyntaxWarning replaces the raised SyntaxWarning).

:kind: test
"""

import warnings


def syntax_warnings(src, mode="eval"):
    with warnings.catch_warnings(record=True) as caught:
        warnings.simplefilter("always")
        compile(src, "<t>", mode)
    return [str(w.message) for w in caught if w.category is SyntaxWarning]


# calling a literal or display
assert syntax_warnings("1(2)") == ["'int' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings("(1, 2)(3)") == ["'tuple' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings("[1](0)") == ["'list' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings("{1: 2}(3)") == ["'dict' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings("{1}(2)") == ["'set' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings('"abc"(1)') == ["'str' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings("None(0)") == ["'NoneType' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings('f"{1}"(0)') == ["'str' object is not callable; perhaps you missed a comma?"]
assert syntax_warnings('t"{1}"(0)') == [
    "'string.templatelib.Template' object is not callable; perhaps you missed a comma?"]

# subscripting a constant that is never subscriptable
assert syntax_warnings("1[0]") == ["'int' object is not subscriptable; perhaps you missed a comma?"]
assert syntax_warnings("1.5[0]") == ["'float' object is not subscriptable; perhaps you missed a comma?"]
assert syntax_warnings("None[0]") == ["'NoneType' object is not subscriptable; perhaps you missed a comma?"]
assert syntax_warnings("...[0]") == ["'ellipsis' object is not subscriptable; perhaps you missed a comma?"]
assert syntax_warnings("True[0]") == ["'bool' object is not subscriptable; perhaps you missed a comma?"]

# subscripting a set display, generator or lambda
assert syntax_warnings("{1, 2}[0]") == ["'set' object is not subscriptable; perhaps you missed a comma?"]
assert syntax_warnings("(lambda: 1)[0]") == ["'function' object is not subscriptable; perhaps you missed a comma?"]
assert syntax_warnings('t"{1}"[0]') == [
    "'string.templatelib.Template' object is not subscriptable; perhaps you missed a comma?"]

# a non-index operand on a subscriptable constant or display
assert syntax_warnings('"ab"["x"]') == [
    "str indices must be integers or slices, not str; perhaps you missed a comma?"]
assert syntax_warnings("(1, 2)[1.5]") == [
    "tuple indices must be integers or slices, not float; perhaps you missed a comma?"]
assert syntax_warnings("[1, 2][None]") == [
    "list indices must be integers or slices, not NoneType; perhaps you missed a comma?"]
assert syntax_warnings('f"a{1}"["x"]') == [
    "str indices must be integers or slices, not str; perhaps you missed a comma?"]

# subscriptable constants and displays do not warn
assert syntax_warnings('"ab"[0]') == []
assert syntax_warnings("b'ab'[0]") == []
assert syntax_warnings("(1, 2)[0]") == []
assert syntax_warnings('f"{1}"[0]') == []
assert syntax_warnings('{"a": 1}["x"]') == []
assert syntax_warnings("[1, 2][0]") == []

# a bool index is an int subclass, and slices are their own shape
assert syntax_warnings('"ab"[True]') == []
assert syntax_warnings("(1, 2)[0:1]") == []
assert syntax_warnings("[1, 2][::2]") == []

# names never infer a type, so variable access does not warn
assert syntax_warnings("n = 5; n[0]", "exec") == []
assert syntax_warnings("n = 5; n(0)", "exec") == []
assert syntax_warnings("n = 'ab'; n['x']", "exec") == []

# the warning fires at compile time even where the code never runs
assert syntax_warnings("def f():\n    return 1[0]", "exec") == [
    "'int' object is not subscriptable; perhaps you missed a comma?"]

# each site warns once, independently
assert len(syntax_warnings("1[0] + 2[0]")) == 2

# runtime behavior is unchanged: the TypeError still comes from running it
try:
    eval(compile("1[0]", "<t>", "eval"))
    assert False
except TypeError:
    pass
try:
    eval(compile('"ab"["x"]', "<t>", "eval"))
    assert False
except TypeError:
    pass
assert eval(compile('"ab"[1]', "<t>", "eval")) == "b"

# an escalated warning is reported as a SyntaxError with the location
warnings.filterwarnings("error")
try:
    compile("1[0]", "<t>", "eval")
    assert False
except SyntaxError as e:
    assert "not subscriptable" in str(e)
warnings.resetwarnings()

print("ok")
