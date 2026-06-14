"""
Tests for function and code object attributes
Exercises PyFunctionObjectType
"""

# Basic function attributes
def my_func(a, b=10, *args, **kwargs):
    """My docstring"""
    return a + b

assert my_func.__name__ == 'my_func'
assert callable(my_func)

# __code__ attribute
code = my_func.__code__
assert code is not None

# __defaults__
assert my_func.__defaults__ == (10,)

# Closure
def make_closure(x):
    def inner():
        return x
    return inner

inner_func = make_closure(42)
assert inner_func() == 42
assert inner_func.__name__ == 'inner'

# Lambda
f = lambda x, y: x + y
assert f(3, 4) == 7
assert f.__name__ == '<lambda>'

# Method attributes
class MyClass:
    def method(self):
        return 42

    @classmethod
    def cm(cls):
        return cls.__name__

    @staticmethod
    def sm():
        return "static"

obj = MyClass()
assert obj.method() == 42
assert MyClass.method(obj) == 42

# classmethod
assert MyClass.cm() == 'MyClass'
assert obj.cm() == 'MyClass'

# staticmethod
assert MyClass.sm() == 'static'
assert obj.sm() == 'static'

print("test_function_attrs passed")
