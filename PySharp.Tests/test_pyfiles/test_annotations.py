"""
Tests for __annotations__ support.
Tests:
- Class variable annotations evaluate to objects (PEP 649 lazy evaluation)
- Module-level variable annotations
- Annotation with initial value
- Multiple annotations
- type.__annotations__ descriptor behavior
- Builtin types raise AttributeError

:kind: test
"""
print("testing annotations")

# Test 1: Class variable annotation
class C:
    x: int
assert C.__annotations__["x"] is int

# Test 2: Annotation with value
class C2:
    x: int = 5
assert C2.__annotations__["x"] is int
assert C2.x == 5

# Test 3: Multiple annotations
class C3:
    a: int
    b: str
    c: float = 3.14
assert C3.__annotations__["a"] is int
assert C3.__annotations__["b"] is str
assert C3.__annotations__["c"] is float
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

# Test 6: Forward reference to the class being defined — the annotation is
# evaluated when __annotations__ is first read, which is after the name binds
class Tree:
    left: Tree
    right: Tree
assert Tree.__annotations__["left"] is Tree
assert Tree.__annotations__["right"] is Tree

# Test 7: String annotations stay strings
class Quoted:
    x: "int"
assert Quoted.__annotations__["x"] == "int"

# Test 8: Annotations inside control flow only count when the branch ran
_cond = False
class Cond1:
    if _cond:
        never: int
assert Cond1.__annotations__ == {}
_cond = True
class Cond2:
    if _cond:
        taken: int
assert Cond2.__annotations__["taken"] is int

# Test 9: Evaluation is deferred until __annotations__ is read
_log = []
def _ann():
    _log.append("evaluated")
    return int
class Lazy:
    v: _ann()
assert _log == [], "annotation must not be evaluated at class creation"
assert Lazy.__annotations__["v"] is int
assert _log == ["evaluated"]

# Test 10: Repeated reads return the same dict
class Stable:
    v: int
assert Stable.__annotations__ is Stable.__annotations__

# Test 11: Class attribute names resolve in the class namespace
class Resolve:
    Args = (int, str)
    v: Args
assert Resolve.__annotations__["v"] == (int, str)

# Test 12: Function body annotations should not crash (no annotation storage)
def func_with_annotation():
    x: int
    return 42

assert func_with_annotation() == 42

# Test 13: Function body annotation with value
def func_with_ann_value():
    x: int = 10
    return x

assert func_with_ann_value() == 10

# Test 14: Function with parameter annotations (not stored yet, no crash)
def func_with_params(x: int, y: str) -> bool:
    return True

assert func_with_params(1, "hello") is True

# Test 15: Multiple functions with annotations
def f1():
    a: str = "hello"
    return a

def f2():
    b: float = 3.14
    return b

assert f1() == "hello"
assert f2() == 3.14

# Test 16: Nested function with annotations
def outer():
    x: int = 1
    def inner():
        y: int = 2
        return y
    return x + inner()

assert outer() == 3

print("test_annotations passed")
