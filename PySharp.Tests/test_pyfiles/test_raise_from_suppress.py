"""
`raise X from Y` must set __suppress_context__ = True for any
explicit cause, an exception or None alike (CPython PyErr_SetCause).
Previously only `from None` suppressed the implicit context; `from <exc>`
left it False while the __cause__/__context__ chain was correct.

:kind: test
"""

try:
    try:
        raise ValueError("inner")
    except ValueError as e:
        raise TypeError("outer") from e
except TypeError as t:
    assert repr(t.__cause__) == "ValueError('inner')"
    assert t.__suppress_context__ is True
    assert t.__context__ is t.__cause__
else:
    raise AssertionError("from-exc did not raise")

# implicit context (no from) keeps suppress False with a context set
try:
    try:
        raise ValueError("inner")
    except ValueError:
        raise TypeError("outer")
except TypeError as t:
    assert t.__suppress_context__ is False
    assert t.__context__ is not None
    assert t.__cause__ is None
else:
    raise AssertionError("implicit chain did not raise")

# from None: cause None, context kept, suppressed
try:
    try:
        raise ValueError("inner")
    except ValueError:
        raise TypeError("outer") from None
except TypeError as t:
    assert t.__cause__ is None
    assert t.__suppress_context__ is True
    assert t.__context__ is not None
else:
    raise AssertionError("from-None did not raise")

# fresh exceptions default to False
assert TypeError("x").__suppress_context__ is False

# a non-exception cause object is rejected (CPython PyErr_SetCause)
try:
    raise TypeError("x") from 42
except TypeError as t:
    assert str(t) == "exception causes must derive from BaseException", str(t)
else:
    raise AssertionError("from-42 accepted a non-exception cause")

# a non-exception raise target keeps its own message
try:
    raise 42
except TypeError as t:
    assert str(t) == "exceptions must derive from BaseException", str(t)
else:
    raise AssertionError("raise 42 accepted a non-exception")

# re-raising with from None clears a previous cause and stays suppressed
def make():
    try:
        raise ValueError("v")
    except ValueError as e:
        return TypeError("t"), e

orig, ve = make()
try:
    raise orig from ve
except TypeError as t:
    assert repr(t.__cause__) == "ValueError('v')"
    assert t.__suppress_context__ is True

try:
    raise orig from None
except TypeError as t:
    assert t.__cause__ is None
    assert t.__suppress_context__ is True

# a plain re-raise (no from) keeps the exception untouched
orig2, ve2 = make()
try:
    raise orig2 from ve2
except TypeError as t:
    pass

# a plain re-raise (no from) keeps the previously set cause intact
orig3, ve3 = make()
try:
    raise orig3 from ve3
except TypeError as t:
    assert repr(t.__cause__) == "ValueError('v')"
    assert t.__suppress_context__ is True

try:
    raise orig3
except TypeError as t:
    assert repr(t.__cause__) == "ValueError('v')"
    assert t.__suppress_context__ is True

print("test_raise_from_suppress passed")
