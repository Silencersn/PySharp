"""
Function argument tests - exercises PyArgsValidator and argument handling
"""

# Too many positional args to built-in
try:
    abs(1, 2)
    assert False, "Should raise TypeError"
except TypeError:
    pass

# Too few positional args to built-in
try:
    chr()
    assert False, "Should raise TypeError"
except TypeError:
    pass

# Unexpected keyword argument
try:
    abs(1, unexpected=2)
    assert False, "Should raise TypeError"
except TypeError:
    pass

# Wrong argument type to built-in
try:
    len(123)
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    int("abc")
    assert False, "Should raise ValueError"
except ValueError:
    pass

# Custom function with default args
def func_default(a, b=10, c=20):
    return a + b + c

assert func_default(1) == 31
assert func_default(1, 2) == 23
assert func_default(1, 2, 3) == 6
assert func_default(1, c=5) == 16
assert func_default(1, b=3, c=4) == 8

# Custom function with *args
def func_varargs(*args):
    return sum(args)

assert func_varargs(1, 2, 3) == 6
assert func_varargs() == 0

# Custom function with **kwargs
def func_kwargs(**kwargs):
    return kwargs['a'] + kwargs.get('b', 0)

assert func_kwargs(a=5, b=3) == 8
assert func_kwargs(a=10) == 10

# Mixed args
def func_mixed(a, b=0, *args, **kwargs):
    return a + b + sum(args) + kwargs.get('c', 0)

assert func_mixed(1) == 1
assert func_mixed(1, 2) == 3
assert func_mixed(1, 2, 3, 4) == 10
assert func_mixed(1, 2, c=5) == 8

# Calling methods with wrong args
try:
    [1, 2, 3].append()
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    [1, 2, 3].append(4, 5)
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    "hello".upper("extra")
    assert False, "Should raise TypeError"
except TypeError:
    pass

# Keyword arguments to built-in functions
assert int("10", base=16) == 16

# Sorted with key as keyword
assert sorted([3, 1, 2], key=lambda x: -x) == [3, 2, 1]

print("test_function_args passed")
