"""
round() on int must work like CPython long_round in every
ndigits form. PySharp used to throw a message-less TypeError for every
round(int) call (with or without ndigits) because int had no __round__
slot at all, and the slot-missing fallback raised TypeError with an
empty message.

CPython semantics pinned here: round(2) == 2, round(True) == 1 - a pooled
exact int, never the bool singleton (long.__round__ follows __int__) -
and identity is preserved for exact ints (round(2) is 2). The fallback
for types without __round__ carries CPython's message: round("x") raises
TypeError("type str doesn't define __round__ method"), never an empty one.

:kind: test
"""

two = 2
minus_three = -3


# red cases: round(int) with and without ndigits
assert round(two) == 2
assert round(minus_three) == -3
assert round(two, 0) == 2
assert round(two, 1) == 2
assert round(two, None) == 2
assert round(True) == 1
assert round(False) == 0
assert round(True, 1) == 1
assert round(True) is not True


# exact int semantics: bool converts, identity holds for exact ints
assert type(round(True)).__name__ == "int"
assert type(round(two)).__name__ == "int"
assert round(two) is two
assert round(two, 1) is two


# guards: floats keep working
assert round(0.5) == 0
assert round(2.675, 2) == 2.67
assert round(-2.5) == -2


# fallback message: non-empty, CPython shape
try:
    round("x")
    assert False, "should raise TypeError"
except TypeError as e:
    assert "doesn't define" in str(e), str(e)
    assert "__round__" in str(e), str(e)


# ndigits must be indexable
try:
    round(two, 1.5)
    assert False, "should raise TypeError"
except TypeError as e:
    assert str(e), "error message must not be empty"

print("test_int_round passed")
