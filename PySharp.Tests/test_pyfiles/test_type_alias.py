"""
Tests for type alias statement (Python 3.12+) - covers PyTypeAliasTypeObject
"""
print("testing type alias")

# Basic type alias
type MyInt = int
x: MyInt = 42
assert isinstance(x, int)
assert x == 42

# Type alias with union
# TODO: isinstance with types.UnionType not yet supported
type MyStrOrInt = str | int
# assert isinstance(42, MyStrOrInt)
# assert isinstance("hello", MyStrOrInt)
# assert not isinstance(3.14, MyStrOrInt)

# Type alias used as annotation
type Vector = list[float]
v: Vector = [1.0, 2.0, 3.0]
assert isinstance(v, list)
assert len(v) == 3

# Using type() builtin on type alias value
# TODO: TypeAliasType is not callable yet
# type AliasType = type
# t = AliasType(42)
# assert t is int

# Type alias for complex type
# TODO: isinstance with types.UnionType not yet supported
# type Number = int | float
# def add_one(x: Number) -> Number:
#     return x + 1
# assert add_one(5) == 6
# assert add_one(3.14) > 4.0

print("test_type_alias passed")
