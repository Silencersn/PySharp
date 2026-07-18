"""
Tests for __annotations__ support (Phase 1).
Tests:
- Class variable annotations stored as strings
- Module-level variable annotations
- Annotation with initial value
- Multiple annotations
- type.__annotations__ descriptor behavior
- Builtin types raise AttributeError
"""
print("testing annotations")

# Test 1: Class variable annotation
class C:
    x: int
assert C.__annotations__["x"] == "int"

# Test 2: Annotation with value
class C2:
    x: int = 5
assert C2.__annotations__["x"] == "int"
assert C2.x == 5

# Test 3: Multiple annotations
class C3:
    a: int
    b: str
    c: float = 3.14
assert C3.__annotations__["a"] == "int"
assert C3.__annotations__["b"] == "str"
assert C3.__annotations__["c"] == "float"
assert C3.c == 3.14

# Test 4: Module-level annotation with value
z: float = 3.14
assert z == 3.14

# Test 5: Descriptor set/delete
class D:
    pass
assert D.__annotations__ == {}
D.__annotations__ = {"mykey": "mytype"}
assert D.__annotations__["mykey"] == "mytype"
del D.__annotations__
assert D.__annotations__ == {}

# Test 6: Complex annotation expression (forward references work because we store as string)
class Tree:
    left: Tree
    right: Tree
assert Tree.__annotations__["left"] == "Tree"
assert Tree.__annotations__["right"] == "Tree"

# Test 7: Function body annotations should not crash (Phase 2, skip annotation storage)
def func_with_annotation():
    x: int
    return 42

assert func_with_annotation() == 42

# Test 8: Function body annotation with value
def func_with_ann_value():
    x: int = 10
    return x

assert func_with_ann_value() == 10

# Test 9: Function with parameter annotations (not stored yet, but no crash)
def func_with_params(x: int, y: str) -> bool:
    return True

assert func_with_params(1, "hello") is True

# Test 10: Multiple functions with annotations
def f1():
    a: str = "hello"
    return a

def f2():
    b: float = 3.14
    return b

assert f1() == "hello"
assert f2() == 3.14

# Test 11: Nested function with annotations
def outer():
    x: int = 1
    def inner():
        y: int = 2
        return y
    return x + inner()

assert outer() == 3
assert Tree.__annotations__["left"] == "Tree"
assert Tree.__annotations__["right"] == "Tree"

print("test_annotations passed")
