"""
Extended exception handling tests - more patterns and error types
"""

# Multiple except clauses
try:
    x = int("not a number")
except ValueError:
    pass
except TypeError:
    assert False, "Should not reach here"

# Bare except
try:
    raise RuntimeError("test")
except:
    pass

# try-finally (no except)
finally_ran = False
try:
    pass
finally:
    finally_ran = True
assert finally_ran is True

# Finally always runs after exception
finally_ran = False
try:
    raise ValueError("test")
except ValueError:
    pass
finally:
    finally_ran = True
assert finally_ran is True

# Exception with message
try:
    raise ValueError("custom message")
except ValueError as e:
    pass

# Raising and catching TypeError
try:
    1 + "string"
    assert False, "Should raise TypeError"
except TypeError:
    pass

# Raising and catching IndexError
try:
    [1, 2, 3][10]
    assert False, "Should raise IndexError"
except IndexError:
    pass

# Raising and catching KeyError
try:
    d = {}
    d['missing']
    assert False, "Should raise KeyError"
except KeyError:
    pass

# Raising and catching AttributeError
try:
    x = 1
    x.does_not_exist
    assert False, "Should raise AttributeError"
except AttributeError:
    pass

# Raising and catching ZeroDivisionError
try:
    1 / 0
    assert False, "Should raise ZeroDivisionError"
except ZeroDivisionError:
    pass

# Raising and catching StopIteration
try:
    it = iter([])
    next(it)
    assert False, "Should raise StopIteration"
except StopIteration:
    pass

# Assert statement
assert True
try:
    assert False, "assertion message"
    assert False, "Should raise AssertionError"
except AssertionError:
    pass

# Nested exception handlers
try:
    try:
        raise ValueError("inner")
    except ValueError:
        pass
    raise TypeError("outer")
except TypeError:
    pass

# Exception in loop
for i in range(5):
    try:
        if i == 3:
            raise RuntimeError("at three")
    except RuntimeError:
        assert i == 3

print("test_exception_extended passed")
