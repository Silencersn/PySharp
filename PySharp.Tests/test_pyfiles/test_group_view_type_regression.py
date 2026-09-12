"""
Regression: three CPython semantics alignments:
- ExceptionGroup constructor reports non-exception items with their
  0-based index
- dict view objects (keys/items/values) repr as content strings like
  dict_keys([...]), including self-referential dicts
- type() and class statements reject the same base appearing twice
"""

# ExceptionGroup item index is 0-based
try:
    ExceptionGroup("g", [5])
    raise AssertionError("non-exception item accepted")
except ValueError as e:
    assert str(e) == "Item 0 of second argument (exceptions) is not an exception", str(e)

try:
    ExceptionGroup("g", ["x", 5])
    raise AssertionError("non-exception head accepted")
except ValueError as e:
    assert str(e) == "Item 0 of second argument (exceptions) is not an exception", str(e)

try:
    ExceptionGroup("g", [ValueError("v"), 5, KeyError("k")])
    raise AssertionError("mid non-exception accepted")
except ValueError as e:
    assert str(e) == "Item 1 of second argument (exceptions) is not an exception", str(e)

# dict view reprs
d = {"a": 1, "b": 2}
assert repr(d.keys()) == "dict_keys(['a', 'b'])", repr(d.keys())
assert str(d.keys()) == "dict_keys(['a', 'b'])"
assert repr(d.items()) == "dict_items([('a', 1), ('b', 2)])", repr(d.items())
assert repr(d.values()) == "dict_values([1, 2])", repr(d.values())
assert repr({}.keys()) == "dict_keys([])"
assert repr({}.items()) == "dict_items([])"
assert repr({}.values()) == "dict_values([])"

# element reprs go through the normal repr protocol
d2 = {"x": [1, 2], "y": "s"}
assert repr(d2.values()) == "dict_values([[1, 2], 's'])", repr(d2.values())
assert repr(d2.keys()) == "dict_keys(['x', 'y'])"

# self-referential dicts render with the cycle marker
d3 = {}
d3[1] = d3
assert repr(d3.items()) == "dict_items([(1, {1: {...}})])", repr(d3.items())

# duplicate base classes are rejected (type() and class statements)
try:
    type("J", (object, object), {})
    raise AssertionError("duplicate object bases accepted")
except TypeError as e:
    assert str(e) == "duplicate base class object", str(e)


class A:
    pass


try:
    type("J", (A, A), {})
    raise AssertionError("duplicate user bases accepted")
except TypeError as e:
    assert str(e) == "duplicate base class A", str(e)

try:
    class J(A, A):
        pass
    raise AssertionError("duplicate class-statement bases accepted")
except TypeError as e:
    assert str(e) == "duplicate base class A", str(e)

print("test_group_view_type_regression passed")
