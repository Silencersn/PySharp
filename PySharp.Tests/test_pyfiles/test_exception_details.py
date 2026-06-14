"""
Tests for exception details - args, repr, str, chaining, with_traceback
Exercises PyBaseExceptionObjectType, PyExceptionObject
"""

# Exception with multiple args
try:
    raise ValueError("test", "message", 42)
except ValueError as e:
    assert len(e.args) == 3
    assert e.args[0] == "test"
    assert e.args[1] == "message"
    assert e.args[2] == 42

# Exception with single arg
try:
    raise RuntimeError("single arg")
except RuntimeError as e:
    assert len(e.args) == 1
    assert e.args[0] == "single arg"

# Exception with no args
try:
    raise ValueError()
except ValueError:
    pass

# str() of exception
try:
    raise ValueError("something went wrong")
except ValueError as e:
    s = str(e)
    assert s == "something went wrong"

# repr() of exception
try:
    raise TypeError("bad type")
except TypeError as e:
    r = repr(e)
    assert isinstance(r, str)
    assert "TypeError" in r

# __suppress_context__
try:
    raise ValueError("test")
except ValueError as e:
    assert e.__suppress_context__ is False

# Exception chaining with 'from'
try:
    try:
        raise ValueError("inner error")
    except ValueError as inner_err:
        raise RuntimeError("outer error") from inner_err
except RuntimeError as e:
    assert e.args[0] == "outer error"
    assert e.__cause__ is not None
    assert e.__cause__.args[0] == "inner error"

# Exception without chaining (context)
try:
    try:
        raise ValueError("original")
    except ValueError:
        raise TypeError("secondary")
except TypeError as e:
    assert e.args[0] == "secondary"
    assert e.__context__ is not None
    assert e.__context__.args[0] == "original"

# No cause by default
try:
    raise ValueError("simple")
except ValueError as e:
    assert e.__cause__ is None

# try/except with exception variable
try:
    1/0
except ZeroDivisionError as e:
    assert isinstance(e, Exception)
    assert str(e) != ""

# Base exception types
assert issubclass(ValueError, Exception)
assert issubclass(TypeError, Exception)
assert issubclass(RuntimeError, Exception)
assert issubclass(KeyError, LookupError)
assert issubclass(IndexError, LookupError)
assert issubclass(ZeroDivisionError, ArithmeticError)
assert issubclass(ArithmeticError, Exception)
assert issubclass(LookupError, Exception)

# Exception hierarchy - multiple levels
assert issubclass(UnboundLocalError, NameError)
assert issubclass(NameError, Exception)

print("test_exception_details passed")
