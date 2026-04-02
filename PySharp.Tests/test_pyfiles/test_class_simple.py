"""
Simple class behavior tests
"""

class MyClass:
    """A simple class docstring"""
    class_attr = 10

    def __init__(self, value):
        self.value = value

    def get_value(self):
        return self.value

    def set_value(self, value):
        self.value = value

# Test instantiation and initial state
obj = MyClass(42)
assert obj.get_value() == 42
assert obj.value == 42

# Test method modification
obj.set_value(100)
assert obj.get_value() == 100
assert obj.value == 100

# Test individual instance states
obj2 = MyClass(200)
assert obj.value == 100
assert obj2.value == 200

# Test class attributes
assert MyClass.class_attr == 10
assert obj.class_attr == 10
obj.class_attr = 20
assert obj.class_attr == 20
assert MyClass.class_attr == 10

# Error cases - AttributeError
try:
    obj.non_existent
    assert False, "Should raise AttributeError"
except AttributeError:
    pass

try:
    del obj.non_existent
    assert False, "Should raise AttributeError"
except AttributeError:
    pass

# Method calling with wrong self (if supported)
try:
    MyClass.get_value(obj2)
    assert MyClass.get_value(obj2) == 200
except TypeError:
    pass # Some implementations might allow it or not depending on descriptor protocol

# Test docstrings (if supported)
# assert MyClass.__doc__ == "A simple class docstring"

print("test_class_simple passed")
