"""
Tests for `not` expression
"""
assert not False is True
assert not True is False
assert not 0 is True
assert not 1 is False
assert not None is True
assert not [] is True
assert not {} is True
assert not "" is True
assert not [1] is False
assert not "x" is False
assert not not True is True
assert not not 0 is False

def check(x):
    if not x:
        return "falsy"
    return "truthy"

assert check(0) == "falsy"
assert check(1) == "truthy"
assert check(None) == "falsy"

f = lambda x: not x
assert f(True) is False
assert f(False) is True

print("test_not_expr passed")
