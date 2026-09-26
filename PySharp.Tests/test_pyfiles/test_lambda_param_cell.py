"""Verifies that lambda parameters captured by nested lambdas or generator expressions are cell-ized like def parameters, so closure packing carries a real cell instead of aborting, including positional-only/keyword-only parameters and lazy genexp cells.

:kind: test
"""

# Regression test: lambda parameters captured by nested function objects.
#
# A lambda parameter referenced by an inner lambda or by a generator
# expression must be cell-ized like a def parameter: the lambda scope owns a
# CellVars prologue, so building the nested function object packs a real cell
# into its closure. Before the fix the parameter stayed a plain value and the
# closure packing aborted the process; the equivalent def forms always worked.
#
# CPython 3.14 references: MAKE_FUNCTION + LOAD_DEREF treat lambda and def
# identically — the captured parameter is a cell from function entry.

# 1. direct lambda-in-lambda: outer parameter captured by inner lambda
g = (lambda f: lambda n: f(n))(lambda x: x * 2)
assert g(5) == 10

add = lambda x: lambda y: x + y
assert add(10)(5) == 15

# immediate call of the inner lambda
assert (lambda x: (lambda y: x + y)(5))(10) == 15

# three nesting levels capture through two cell hops
assert (lambda x: lambda y: lambda z: x + y + z)(1)(2)(3) == 6

# inner default value is evaluated from the outer parameter's cell
g5 = (lambda x: lambda y=x: y)(7)
assert g5() == 7

# each call of the factory gets fresh cells
mk = lambda v: lambda: v
fa, fb = mk(1), mk(2)
assert (fa(), fb()) == (1, 2)

# inner lambda selected by a conditional expression still closes over the cell
assert (lambda a: (lambda: a) if a > 0 else (lambda: -a))(3)() == 3

# 2. generator expression in a lambda body referencing the parameter: the
# genexp is built with MAKE_FUNCTION and packs the parameter's cell
assert (lambda outer: list(y + outer for y in [10, 20]))(5) == [15, 25]
assert (lambda a: max(b * a for b in range(5)))(3) == 12
assert (lambda a: sum(x * a for x in range(4)))(2) == 12
assert list((lambda a: (x + a for x in "ab"))("!")) == ["a!", "b!"]

# lazy genexp keeps its own cell even after the factory ran again
f8 = lambda a: (i + a for i in range(3))
g8 = f8(10)
f8(100)
assert list(g8) == [10, 11, 12]

# genexp nested in an inner lambda captures both parameters
f10 = (lambda a: lambda b: list(a * b for _ in [0]))(3)
assert f10(4) == [12]

# 3. eager comprehensions in a lambda body (must stay working)
assert (lambda outer: [y + outer for y in [10, 20]])(5) == [15, 25]
assert (lambda a: {k: k * a for k in [1, 2]})(3) == {1: 3, 2: 6}
assert (lambda a: {k * a for k in [1, 2]})(3) == {3, 6}

# 4. mixed: nested lambda and eager genexp capture the same cell together
f12 = (lambda a: (lambda: a + 1, list(a * 2 for _ in [0])))(5)
assert (f12[0](), f12[1]) == (6, [10])

# 5. parity with def forms and interaction with other scopes
def d(p):
    return lambda q: lambda r: p + q + r
assert d(1)(2)(3) == 6

# inner parameter shadows the outer one; the outer cell stays untouched
assert (lambda x: (lambda x: x + 1)(5) + x)(10) == 16

# global captured alongside the lambda parameter
G = 2
assert (lambda a: lambda: a * G)(3)() == 6

# positional-only and keyword-only parameters are captured as cells too
f16 = (lambda a, /, b, *, c: lambda: a + b + c)(1, 2, c=3)
assert f16() == 6

# the returned closure actually exposes the packed cell
inner = (lambda a: lambda: a)(5)
assert inner.__closure__ is not None and len(inner.__closure__) == 1

# curried/self-application style still yields a callable, not a crash
fib = (lambda f: f(15))(lambda f: (lambda n: 1 if n <= 1 else f(n-1) + f(n-2)))
assert repr(fib).startswith("<function")

print("test_lambda_param_cell passed")
