"""
Tests for generic class support (PEP 695).
Tests:
- Basic generic class definition with type params
- Subscript on generic class: Foo[int]
- Instantiation of parameterized generic: Foo[int]()
- Multiple type parameters
- Generic alias properties: __origin__, __args__
- Import of typing.Generic
"""
print("testing generic class")

# Test 1: Basic generic class with pass body
class Box[T]:
    pass

# Test 2: Subscript on generic class returns a GenericAlias
IntBox = Box[int]
assert IntBox is not Box

# Test 3: GenericAlias has __origin__ and __args__
assert IntBox.__origin__ is Box
assert IntBox.__origin__ is not None
assert IntBox.__args__ == (int,)

# Test 4: Instantiate parameterized generic
b = Box[int]()
assert b is not None

# Test 5: Multiple type parameters
class Pair[T, U]:
    pass

IntStrPair = Pair[int, str]
assert IntStrPair.__origin__ is Pair
assert IntStrPair.__args__ == (int, str)

# Test 6: Single tuple arg
class Container[T]:
    pass

c = Container[int]()
assert c is not None

# Test 7: typing module exports Generic
import typing
assert typing.Generic is not None
assert typing.Generic.__class_getitem__ is not None

# Test 8: Class inheriting from typing.Generic
# (This test is for when Generic base support is fully implemented)
# class MyGeneric(typing.Generic):
#     pass

print("test_generic_class passed")
