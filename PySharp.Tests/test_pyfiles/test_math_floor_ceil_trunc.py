"""
math.floor/ceil/trunc must return int (not float) and follow
the CPython 3.14 dispatch - an exact float converts straight to int with
the conversion errors for inf/nan, other objects try __floor__/__ceil__/
__trunc__ first (math.floor/ceil fall back to a float/index conversion,
math.trunc does not), and int defines the three methods as its identity
(bool converts to a pooled exact int).

:kind: test
"""

import math


# direct slot calls and neighbouring paths unchanged (checked before a
# float subclass with a custom __floor__ is defined below)
assert (2.5).__floor__() == 2
assert (2.5).__ceil__() == 3
assert (-3.7).__trunc__() == -3
assert round(2.5) == 2
assert round(3.7) == 4
assert int(2.9) == 2
assert math.floor(10) // 3 == 3
assert '%d' % math.floor(2.9) == '2'
# float inputs return int
assert math.floor(2.5) == 2
assert math.ceil(2.1) == 3
assert math.floor(-2.5) == -3
assert math.trunc(-3.7) == -3
assert math.ceil(-0.5) == 0
assert math.trunc(-0.5) == 0
assert math.floor(1e300) == int(1e300)
assert type(math.floor(2.5)) is int
assert type(math.ceil(2.1)) is int
assert type(math.trunc(-3.7)) is int

# non-finite floats report the conversion errors
for value in (float('inf'), float('-inf')):
    for func in (math.floor, math.ceil, math.trunc):
        try:
            func(value)
        except OverflowError as e:
            assert str(e) == "cannot convert float infinity to integer", str(e)
        else:
            raise AssertionError("expected OverflowError")

for func in (math.floor, math.ceil, math.trunc):
    try:
        func(float('nan'))
    except ValueError as e:
        assert str(e) == "cannot convert float NaN to integer", str(e)
    else:
        raise AssertionError("expected ValueError")

# int inputs are accepted (identity, bool converts)
assert math.floor(5) == 5
assert math.ceil(5) == 5
assert math.trunc(5) == 5
assert math.floor(True) == 1
assert math.trunc(True) == 1
assert (math.floor(True) is True) is False

# the protocol methods are honoured for non-float objects
class HasFloor:
    def __floor__(self):
        return 42

assert math.floor(HasFloor()) == 42
try:
    math.ceil(HasFloor())
except TypeError as e:
    assert str(e) == "must be real number, not HasFloor", str(e)
else:
    raise AssertionError("expected TypeError")


class HasTrunc:
    def __trunc__(self):
        return 7

assert math.trunc(HasTrunc()) == 7

# math.floor/ceil fall back to a float/index conversion, math.trunc
# reports the missing method instead
class Idx:
    def __index__(self):
        return 7

assert math.floor(Idx()) == 7
assert math.ceil(Idx()) == 7
try:
    math.trunc(Idx())
except TypeError as e:
    assert str(e) == "type Idx doesn't define __trunc__ method", str(e)
else:
    raise AssertionError("expected TypeError")

try:
    math.floor('x')
except TypeError as e:
    assert str(e) == "must be real number, not str", str(e)
else:
    raise AssertionError("expected TypeError")


# float subclasses inherit the slots but can override the protocol
class F(float):
    pass

assert math.floor(F(2.5)) == 2
assert type(math.floor(F(2.5))) is int
assert math.floor(F(True)) == 1


class CF(float):
    def __floor__(self):
        return 99

assert math.floor(CF(2.5)) == 99


# int subclasses convert to exact ints
class I(int):
    pass

assert math.floor(I(5)) == 5
assert type(math.floor(I(5))) is int


print("test_math_floor_ceil_trunc passed")
