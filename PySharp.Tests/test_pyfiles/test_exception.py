"""
Exception handling tests (try-except-finally-else, raise)

:kind: test
"""

# Simple try-except
try:
    x = 1 / 0
    assert False, "ZeroDivisionError should be raised"
except ZeroDivisionError:
    pass

# Exception with value check
try:
    int("abc")
except ValueError:
    pass

# try-except-else
try:
    x = 1 + 1
except:
    assert False, "Exception should not be raised"
else:
    assert x == 2

# try-except-finally
finally_executed = False
try:
    try:
        raise RuntimeError("test error")
    except RuntimeError:
        pass
    finally:
        finally_executed = True
except:
    pass
assert finally_executed is True

# Nested try-except
try:
    try:
        raise KeyError("key")
    except ValueError:
        assert False, "Should not catch KeyError"
except KeyError:
    pass

print("test_exception passed")
