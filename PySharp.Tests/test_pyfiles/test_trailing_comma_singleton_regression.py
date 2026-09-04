"""
Regression: a star_expressions list with exactly one element followed by a
trailing comma must build a single-element tuple, not silently degenerate
into the bare element.

CPython 3.14 reference (Grammar/python.gram:717-721):
    x = 1,                  -> x == (1,)
    def f(): return 1,      -> returns (1,)
    def g(): yield 1,       -> yields (1,)
    for i in 1,:            -> iterates (1,)
    x = *[1, 2],            -> (1, 2)
    f = lambda: 1,          -> f is a 1-tuple containing the lambda
    y += 1, (y is int)      -> TypeError: unsupported operand type(s)

Affected positions: assignment / augmented-assignment RHS, return, yield,
for iterables and lambda bodies. A trailing '|' in an or-pattern
(`case a|:`) hits the same helper and must be a SyntaxError.
"""

# --- assignment RHS ---
x = 1,
assert isinstance(x, tuple)
assert x == (1,)
assert len(x) == 1 and x[0] == 1

# a starred element unpacks into the tuple being built
x = *[1, 2],
assert isinstance(x, tuple)
assert x == (1, 2)

# --- augmented assignment RHS: int += tuple must raise TypeError ---
y = 5
try:
    y += 1,
    assert False, 'should raise TypeError'
except TypeError:
    pass

# --- return ---
def f():
    return 1,
assert isinstance(f(), tuple)
assert f() == (1,)

def h():
    return *[1, 2],
assert h() == (1, 2)

# --- yield ---
def g():
    yield 1,
assert list(g()) == [(1,)]

def g2():
    yield *[1, 2],
assert list(g2()) == [(1, 2)]

# --- for iterables ---
count = 0
for i in [1, 2],:
    count += 1
assert count == 1 and i == [1, 2]

got = []
for i in 1,:
    got.append(i)
assert got == [1]

# --- lambda body ---
lam = lambda: 1,
assert isinstance(lam, tuple)
assert len(lam) == 1
assert callable(lam[0]) and lam[0]() == 1
try:
    lam()
    assert False, 'should raise TypeError'
except TypeError:
    pass

# --- or-pattern: trailing '|' is a SyntaxError ---
try:
    compile("match 5:\n    case a|: pass", "<test>", "exec")
    assert False, 'trailing | in or-pattern should raise SyntaxError'
except SyntaxError:
    pass

# --- guards: forms that must keep working ---
multi = 1, 2
assert multi == (1, 2)

star3 = *[1, 2], 3
assert star3 == (1, 2, 3)

star4 = *[1], *[2]
assert star4 == (1, 2)

cont = 1, \
    2,
assert cont == (1, 2)

assert (*[1, 2],) == (1, 2)
assert [1,] == [1]
assert {1: 2,} == {1: 2}

d = {}
d[9,] = 1
assert (9,) in d and d[(9,)] == 1

print("test_trailing_comma_singleton_regression passed")
