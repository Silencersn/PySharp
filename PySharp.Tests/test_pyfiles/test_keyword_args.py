"""
Keyword arguments and *args, **kwargs tests

:kind: test
"""

def func(a, b, c=10):
    return a + b + c

assert func(1, 2) == 13
assert func(1, 2, 3) == 6
assert func(a=1, b=2, c=3) == 6
assert func(b=5, a=2) == 17

def var_args(*args, **kwargs):
    return len(args), len(kwargs)

assert var_args(1, 2, 3) == (3, 0)
assert var_args(x=1, y=2) == (0, 2)
assert var_args(1, z=3) == (1, 1)

print("test_keyword_args passed")
