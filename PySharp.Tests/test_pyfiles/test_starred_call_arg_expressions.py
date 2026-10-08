"""
Call arguments and subscripts take '*'/'**' before a full expression

:kind: test
"""
def f(*args, **kwds):
    return (args, kwds)

a = [1, 2]
b = {"x": 9}

# '*' before a boolean expression
assert f(*a or [7]) == ((1, 2), {})
assert f(*[] or a) == ((1, 2), {})
assert f(*a and [42]) == ((42,), {})

# '*' before a conditional expression
assert f(*(a if a else [5])) == ((1, 2), {})
assert f(*[5] if False else a) == ((1, 2), {})

# '*' mixed with plain arguments, '**' before a boolean expression
assert f(0, *a, **b or {"y": 8}) == ((0, 1, 2), {"x": 9})
assert f(**{} or b) == ((), {"x": 9})

# '*' in a subscript is the same full-expression level; the unpacked
# form reaches the index as a tuple, which lists reject at runtime
d = [10, 20, 30]
raised = False
try:
    d[*[1] or [2]]
except TypeError:
    raised = True
assert raised

# generator unpacking before a boolean expression
def gen():
    yield 3
    yield 4

assert f(*gen() and [1]) == ((1,), {})
