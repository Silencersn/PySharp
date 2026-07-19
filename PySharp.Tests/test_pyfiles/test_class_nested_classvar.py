"""
Tests for __class__ in nested class scopes (free variable propagation).
Tests:
- __class__ in nested class body raises NameError (cell empty, matches CPython)
- __class__ in regular method
- __class__ via method in nested class
"""
print("testing nested class __class__")

# Test 1: __class__ in nested class body
# CPython: NameError: cannot access free variable '__class__'
#          where it is not associated with a value in enclosing scope
# PySharp: NameError: cannot access local or free variable '__class__'
#          where it is not associated with a value
try:
    class Outer:
        class Inner:
            x = __class__
    assert False, "should have raised"
except NameError:
    pass

# Test 2: __class__ in regular method
class WithMethod:
    def get_class(self):
        return __class__

assert WithMethod().get_class() is WithMethod

# Test 3: __class__ via method in nested class
class Outer2:
    class Inner2:
        def get_outer(self):
            return __class__

obj = Outer2.Inner2()
assert obj.get_outer() is Outer2.Inner2

# Test 4: __class__ in deeply nested class -> method
class Level1:
    class Level2:
        class Level3:
            def get_class(self):
                return __class__

assert Level1.Level2.Level3().get_class() is Level1.Level2.Level3

# Test 5: __class__ in classmethod of nested class
class Outer3:
    class Inner3:
        @classmethod
        def get_class(cls):
            return __class__

assert Outer3.Inner3.get_class() is Outer3.Inner3

print("test_class_nested_classvar passed")
