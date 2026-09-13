# Regression test: closures capturing comprehension loop variables.
#
# Three compiler paths must all provide a cell for the comprehension target:
#   1. a lambda body inlining a comprehension whose target is captured by a
#      nested lambda (the outer lambda's CellVars prologue was missing);
#   2. a generator expression whose target is captured by a nested lambda
#      (the genexpr prologue was missing);
#   3. a class-body comprehension whose target is captured by a nested lambda
#      (the inline comprehension scope did not participate in capture
#      analysis, so the name leaked to the global scope).
#
# CPython 3.14 references: the comprehension owns the cellvar, so every
# captured closure observes the final loop value.

# 1. lambda-in-lambda through an inlined comprehension
g = lambda: [f() for f in [lambda: i for i in range(3)]]
assert g() == [2, 2, 2]

def outer():
    return [f() for f in [lambda: i for i in range(3)]]
assert outer() == [2, 2, 2]

# 2. generator expression: target captured by the element lambda. The genexp
# is lazy, so each lambda is invoked while the loop is paused at its yield —
# closures observe the value bound at their own iteration
gen = (lambda: i for i in range(3))
assert [f() for f in gen] == [0, 1, 2]

class CGen:
    g = (lambda: i for i in range(3))
assert next(CGen.g)() == 0

def outer_gen():
    return (lambda: i for i in range(4))
assert [f() for f in outer_gen()] == [0, 1, 2, 3]

# 3. class-body comprehension: target must resolve to the comprehension
# scope, not leak to the module globals
i = "GLOBAL"
class C:
    fs = [lambda: i for i in range(3)]
assert [f() for f in C.fs] == [2, 2, 2]

class CSet:
    s = {lambda: i for i in range(2)}
assert [f() for f in CSet.s] == [1, 1]

class CDict:
    d = {i: (lambda: i) for i in range(2)}
assert sorted(f() for f in CDict.d.values()) == [1, 1]

# class body inside a function: lambda captures the target and reads an
# enclosing-function free variable through the same closure chain
def fn_class():
    w = 10
    class C:
        fs = [lambda: w + i for i in range(3)]
    return [f() for f in C.fs]
assert fn_class() == [12, 12, 12]

# two comprehensions in one class body keep independent cells
class C2:
    fs1 = [lambda: i for i in range(3)]
    fs2 = [lambda: i for i in range(5)]
assert ([f() for f in C2.fs1], [f() for f in C2.fs2]) == ([2, 2, 2], [4, 4, 4, 4, 4])

# plain comprehensions (no capture) in a class body still resolve normally
class C3:
    xs = [1, 2, 3]
    ys = [x for x in xs if x > 1]
assert C3.ys == [2, 3]

print("test_comprehension_closure_capture_regression passed")
