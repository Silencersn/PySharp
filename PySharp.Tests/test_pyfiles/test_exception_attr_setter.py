"""
BaseException.__cause__, __context__ and __suppress_context__
must be writable like CPython. Assigning an exception to __cause__ also
suppresses the implicit context (PyException_SetCause); None only clears;
__context__ never touches the flag; non-exception values and deletes are
rejected with CPython's exact TypeError messages.

:kind: test
"""

e1 = ValueError("a")
e2 = KeyError("b")


class MyErr(ValueError):
    pass


# plain assignment and readback
e1.__cause__ = e2
assert repr(e1.__cause__) == "KeyError('b')"
e1.__context__ = e2
assert repr(e1.__context__) == "KeyError('b')"

# assigning an exception to __cause__ suppresses the implicit context
assert e1.__suppress_context__ is True

# None clears without touching the sticky flag
e1.__cause__ = None
e1.__context__ = None
assert e1.__cause__ is None
assert e1.__context__ is None
assert e1.__suppress_context__ is True

# custom exception subclass instances are accepted (both sides)
m = MyErr("m")
m.__cause__ = e2
assert repr(m.__cause__) == "KeyError('b')"
e1.__cause__ = m
assert isinstance(e1.__cause__, MyErr)

# __context__ assignment never sets the suppress flag
e3 = ValueError("c")
e3.__context__ = e2
assert e3.__suppress_context__ is False

# __suppress_context__ roundtrip
e3.__suppress_context__ = True
assert e3.__suppress_context__ is True
e3.__suppress_context__ = False
assert e3.__suppress_context__ is False

# non-exception values are rejected with CPython's messages
for attr, msg in [
    ("__cause__", "exception cause must be None or derive from BaseException"),
    ("__context__", "exception context must be None or derive from BaseException"),
]:
    try:
        setattr(e3, attr, 42)
        raise AssertionError(attr + " accepted a non-exception")
    except TypeError as t:
        assert str(t) == msg, str(t)

for val in [1, "x", ValueError]:
    try:
        e3.__suppress_context__ = val
        raise AssertionError("__suppress_context__ accepted a non-bool")
    except TypeError as t:
        assert str(t) == "attribute value type must be bool", str(t)

# deletes are rejected with CPython's messages
for attr, msg in [
    ("__cause__", "__cause__ may not be deleted"),
    ("__context__", "__context__ may not be deleted"),
    ("__suppress_context__", "can't delete numeric/char attribute"),
]:
    try:
        delattr(e3, attr)
        raise AssertionError("del " + attr + " accepted")
    except TypeError as t:
        assert str(t) == msg, str(t)

# a re-raise preserves a manually assigned cause
e5 = ValueError("e")
e5.__cause__ = e2
try:
    raise e5
except ValueError as caught:
    assert repr(caught.__cause__) == "KeyError('b')"

# self-reference is allowed
e5.__cause__ = e5
assert e5.__cause__ is e5

print("test_exception_attr_setter passed")
