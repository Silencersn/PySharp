"""
Edge case tests for function default parameters
"""
# Multiple defaults
def f_multi(a, b=2, c=3, d=4):
    return a + b + c + d

assert f_multi(1) == 10
assert f_multi(1, d=10) == 16

# Default with keyword-only
def f_kwonly(a, *, b=10, c=20):
    return a + b + c

assert f_kwonly(1) == 31
assert f_kwonly(1, b=5) == 26

# Default with *args
def f_varargs(a, b=10, *args):
    return a + b + sum(args)

assert f_varargs(1) == 11
assert f_varargs(1, 2, 3, 4) == 10

# Lambda with defaults
add_default = lambda a, b=10: a + b
assert add_default(5) == 15
assert add_default(5, 20) == 25

assert (lambda x=1, y=2, z=3: x + y + z)() == 6
assert (lambda x=1, y=2, z=3: x + y + z)(10) == 15

print("test_defaults_edge passed")
