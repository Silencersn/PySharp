# Regression: unary slot hooks pass their result through unchecked —
# CPython's unary slot wrappers do not validate __neg__/__pos__/
# __invert__ returns, so a hook returning NotImplemented yields the
# singleton itself, exactly like __abs__ already did. Only a missing
# slot raises the bad-operand TypeError, and the binary reflected
# fallback is untouched.

class AllNotImplemented:
    def __neg__(self):
        return NotImplemented

    def __pos__(self):
        return NotImplemented

    def __invert__(self):
        return NotImplemented

    def __abs__(self):
        return NotImplemented

v = AllNotImplemented()
for expr in ["-v", "+v", "~v", "abs(v)"]:
    got = eval(expr)
    assert got is NotImplemented, (expr, got)
    assert type(got).__name__ == "NotImplementedType", (expr, type(got).__name__)
    assert repr(got) == "NotImplemented", (expr, repr(got))

try:
    bool(-v)
except TypeError as e:
    assert str(e) == "NotImplemented should not be used in a boolean context", str(e)
else:
    raise AssertionError("bool(NotImplemented) must raise")


class NoHooks:
    pass

w = NoHooks()
for expr, op in [("-w", "-"), ("+w", "+"), ("~w", "~")]:
    try:
        eval(expr)
    except TypeError as e:
        assert str(e) == f"bad operand type for unary {op}: 'NoHooks'", str(e)
    else:
        raise AssertionError(expr)
try:
    abs(w)
except TypeError as e:
    assert str(e) == "bad operand type for abs(): 'NoHooks'", str(e)
else:
    raise AssertionError("abs(w)")

class RealNeg:
    def __neg__(self):
        return 42

class StrInvert:
    def __invert__(self):
        return "data"

assert -RealNeg() == 42
assert ~StrInvert() == "data"


class BaseHook:
    def __neg__(self):
        return NotImplemented

class OverrideHook(BaseHook):
    def __neg__(self):
        return 7

assert -OverrideHook() == 7
assert -BaseHook() is NotImplemented

# binary reflected fallback keeps its own semantics: both directions
# NotImplemented -> TypeError, no singleton leakage
class BinaryDeclines:
    def __sub__(self, other):
        return NotImplemented

    def __rsub__(self, other):
        return NotImplemented

b = BinaryDeclines()
for expr, msg in [
    ("b - 1", "unsupported operand type(s) for -: 'BinaryDeclines' and 'int'"),
    ("1 - b", "unsupported operand type(s) for -: 'int' and 'BinaryDeclines'"),
]:
    try:
        eval(expr)
    except TypeError as e:
        assert str(e) == msg, (expr, str(e))
    else:
        raise AssertionError(expr)

print("unary notimplemented faces ok")
print("test_unary_notimplemented_regression passed")
