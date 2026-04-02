"""
User-defined descriptor tests (__get__, labels, and inheritance)
"""

class Desc:
    def __init__(self, name):
        self.name = name
    def __get__(self, instance, owner):
        # returns metadata for testing
        return self.name, instance, owner

class A:
    test_prop = Desc('From A')
    aaa = 123

class B(A):
    test_prop = Desc('From B')
    def __repr__(self):
        # Test super() call in a method
        return super().__repr__()

# Test descriptor lookup on derived class instance
b = B()
name, instance, owner = b.test_prop
assert name == 'From B'
assert isinstance(instance, B)
assert owner is B

# Test descriptor check on builtin methods
t = type(list.append)
assert hasattr(t, '__get__')

print("test_user_defined_descriptor passed")
