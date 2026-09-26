"""
Metaclass behavior tests

:kind: test
"""

class Meta(type):
    """A simple metaclass that adds attributes and handles kwargs"""
    def __new__(cls, name, bases, attrs, **kwargs):
        return super().__new__(cls, name, bases, attrs)

    def __init__(cls, name, bases, attrs, **kwargs):
        super().__init__(name, bases, attrs)
        cls.is_meta = True
        cls.meta_kwarg = kwargs.get('meta_kwarg', 'default')

# Test class creation with metaclass
class MyClass(metaclass=Meta, meta_kwarg='custom'):
    pass

assert hasattr(MyClass, 'is_meta')
assert MyClass.is_meta is True
assert MyClass.meta_kwarg == 'custom'
assert type(MyClass) is Meta

# Test inheritance from a class with a metaclass
class MySubClass(MyClass):
    pass

assert hasattr(MySubClass, 'is_meta')
assert MySubClass.is_meta is True
assert MySubClass.meta_kwarg == 'default'
assert type(MySubClass) is Meta

# Test metaclass conflict (should raise TypeError)
class Meta2(type):
    pass

try:
    class MyConflictClass(MyClass, metaclass=Meta2):
        pass
    assert False, 'TypeError should be raised'
except TypeError:
    pass

# Test metaclass inheritance
class Meta3(Meta):
    pass

class MyClass3(MyClass, metaclass=Meta3):
    pass

assert MyClass3.is_meta is True
assert type(MyClass3) is Meta3

print("test_metaclass passed")
