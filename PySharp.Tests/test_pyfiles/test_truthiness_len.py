"""
truthiness must honor __len__ when __bool__ is absent
(CPython PyObject_IsTrue). Types without __bool__ that define __len__
were always truthy, so bool()/not/if/while/and-or ignored the length.
This also covers the builtin types whose only length source is __len__
(bytes, bytearray, range, memoryview), error propagation from a raising
__len__, and the error messages for invalid __len__/__bool__ returns.

:kind: test
"""

class Zero:
    def __len__(self):
        return 0

class Five:
    def __len__(self):
        return 5

class BothBoolWins(Five):
    def __bool__(self):
        return False

class BothBoolWins2(Zero):
    def __bool__(self):
        return True

# user classes: length drives truthiness without __bool__
assert bool(Zero()) is False
assert bool(Five()) is True
assert not Zero() is True
assert not Five() is False
if Zero():
    raise AssertionError("if took truthy branch for __len__ 0")
if Five():
    pass
else:
    raise AssertionError("if took falsy branch for __len__ 5")

# __bool__ wins over __len__ when both exist
assert bool(BothBoolWins()) is False
assert bool(BothBoolWins2()) is True

# and/or short-circuit through the same truthiness
r = Zero() and "x"
assert type(r) is Zero  # falsy left operand is returned as-is
assert (Five() and "x") == "x"
assert (Zero() or "y") == "y"
r = Five() or "y"
assert type(r) is Five

# builtin types with only a __len__ source
assert bool(b"") is False
assert bool(b"x") is True
assert bool(bytearray()) is False
assert bool(bytearray(b"x")) is True
assert bool(range(0)) is False
assert bool(range(3)) is True
assert bool(memoryview(b"")) is False
assert bool(memoryview(b"ab")) is True
assert not bytearray() is True
assert not range(3) is False

# containers keep their own truthiness
assert bool([]) is False and bool([0]) is True
assert bool({}) is False and bool({1: 2}) is True
assert bool("") is False and bool("a") is True
assert bool(set()) is False and bool({1}) is True
assert bool(()) is False and bool((1,)) is True

# a raising __len__ propagates from truthiness
class LenRaises:
    def __len__(self):
        raise RuntimeError("boom")

try:
    bool(LenRaises())
except RuntimeError as e:
    assert str(e) == "boom"
else:
    raise AssertionError("__len__ exception swallowed by bool()")

# invalid __len__ returns keep len() and bool() consistent
class BadLen:
    def __len__(self):
        return -1

class WrongType:
    def __len__(self):
        return "3"

try:
    bool(BadLen())
except ValueError as e:
    assert str(e) == "__len__() should return >= 0"
else:
    raise AssertionError("negative __len__ accepted by bool()")

try:
    bool(WrongType())
except TypeError as e:
    assert "'str' object cannot be interpreted as an integer" in str(e)
else:
    raise AssertionError("non-int __len__ accepted by bool()")

try:
    len(WrongType())
except TypeError as e:
    assert "'str' object cannot be interpreted as an integer" in str(e)
else:
    raise AssertionError("non-int __len__ accepted by len()")

# invalid __bool__ return message
class BoolWrong:
    def __bool__(self):
        return 1

try:
    bool(BoolWrong())
except TypeError as e:
    assert str(e) == "__bool__ should return bool, returned int"
else:
    raise AssertionError("non-bool __bool__ accepted")

# __len__ returning a bool subclass of int is fine
class LenReturnsBool:
    def __len__(self):
        return True

assert bool(LenReturnsBool()) is True

# while conditions drive loops with the same rules
d = {1: 2}
count = 0
while d:
    d.clear()
    count += 1
assert count == 1

print("test_truthiness_len passed")
