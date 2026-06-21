"""Regression tests: list operations with custom class __eq__."""

class Value:
    def __init__(self, v):
        self.v = v
    def __eq__(self, other):
        return isinstance(other, Value) and self.v == other.v

v1 = Value(1)
v2 = Value(1)
assert v1 == v2
assert id(v1) != id(v2)

print("=== Test 1: list.count with custom __eq__ ===")
a = [Value(1), Value(2), Value(1)]
assert a.count(Value(1)) == 2
print("  PASS")

print("\n=== Test 2: list.remove with custom __eq__ ===")
a = [Value(10), Value(20), Value(30)]
a.remove(Value(20))
assert len(a) == 2
print("  PASS")

print("\n=== Test 3: list.index with custom __eq__ ===")
a = [Value(1), Value(2), Value(3), Value(2)]
assert a.index(Value(2)) == 1
print("  PASS")

print("\nAll tests completed")
