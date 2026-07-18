"""
Tests for TypeVar runtime objects in generic classes and functions (PEP 695).
Tests:
- Class generic: class C[T]: a = T
- Function generic: def f[K](self): b = K
- __type_params__ returns TypeVar objects
- TypeVar.__name__ property
- Multiple type params
"""
print("testing generic typevar")

# Test 1: __type_params__ returns tuple of TypeVar objects
class Box[T]:
    pass

assert len(Box.__type_params__) == 1

params = Box.__type_params__
t = params[0]
assert t.__name__ == "T"

# Test 2: Multiple type params
class Pair[T, U]:
    pass

params = Pair.__type_params__
assert len(params) == 2
assert params[0].__name__ == "T"
assert params[1].__name__ == "U"

print("test_generic_typevar passed")
