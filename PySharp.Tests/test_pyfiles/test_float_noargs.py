"""
float() with no arguments must return 0.0, like CPython's
float_new (Objects/floatobject.c, x == NULL -> 0.0). PySharp used to
raise TypeError: missing 1 required positional argument.

Also pins the str-parsing failure type (ValueError with the CPython
message, not a bare TypeError) and float subclass construction, which
must produce subclass instances (CPython float_new creates the subclass
type when cls is a float subclass).

:kind: test
"""

assert int() == 0                # int() no-arg control (already correct)
assert float() == 0.0            # red case


# red case: an invalid string must raise ValueError with the CPython
# message, not a message-less TypeError
try:
    float("abc")
except ValueError as e:
    assert "could not convert string to float: 'abc'" in str(e), str(e)
except TypeError as e:
    assert False, "must be ValueError, not TypeError: " + str(e)
else:
    assert False, "should raise ValueError"


# red case: float subclass calls must construct subclass instances
class F(float):
    pass


f0 = F()
f15 = F(1.5)
assert isinstance(f0, F) and f0 == 0.0
assert isinstance(f15, F) and f15 == 1.5
assert type(f15).__name__ == "F"


# guards
assert float("3.14") == 3.14
assert float(True) == 1.0
assert float(2) == 2.0
assert float(-2.5) == -2.5
assert float("inf") == float("infinity") == float("Infinity")
assert float("-inf") == float("-Infinity")
nan = float("nan")
assert nan != nan
try:
    float(x=1.5)
except TypeError:
    pass
else:
    assert False, "keyword call must stay a TypeError"

print("test_float_noargs passed")
