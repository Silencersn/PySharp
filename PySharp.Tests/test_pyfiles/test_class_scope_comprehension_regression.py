"""
Regression: names in a class-body comprehension's condition / element /
value expressions must NOT resolve to the class scope. Only the outermost
iterable is evaluated in the class scope; the comprehension body is a
separate scope that skips the class block (CPython symtable rule).

PySharp used to leak the class scope into the inlined comprehension
forms (listcomp / dictcomp / setcomp), so:

    xs = ["g"]
    class C:
        xs = [1, 2, 3]
        ys = [x for x in xs if x > len(xs)]

    CPython: [2, 3]   (condition len(xs) sees the global xs = ["g"])
    PySharp: []       (condition len(xs) wrongly sees the class xs)

The same leak made CPython NameError forms ([xs[i] for ...]) run
silently. Note the genexp form already resolves correctly in PySharp
(separate scope) and is pinned below as a guard.
"""

xs = ["g"]


# red case 1: listcomp condition must skip the class scope
class C1:
    xs = [1, 2, 3]
    ys = [x for x in xs if x > len(xs)]

assert C1.ys == [2, 3]


# red case 2: listcomp element expression must raise NameError, not read
# the class variable
class C2:
    xs = [1, 2]
    try:
        ys = [xs[i] for i in range(2)]
        leaked = True
    except NameError:
        leaked = False

assert not C2.leaked


# red case 3: dictcomp value expression must raise NameError as well
class C3:
    xs = [1, 2]
    try:
        ys = {x: len(xs) for x in xs}
        leaked = True
    except NameError:
        leaked = False

assert not C3.leaked


# red case 4: setcomp condition must skip the class scope too
class C4:
    xs = [1, 2, 3]
    ys = {x for x in xs if x > len(xs)}

assert sorted(C4.ys) == [2, 3]


# --- guards: behavior already matching CPython ---

# genexp body already skips the class scope (must stay correct)
class G1:
    xs = [1, 2, 3]
    ys = tuple(x for x in xs if x > len(xs))

assert G1.ys == (2, 3)


# outermost iterable IS evaluated in the class scope
class G2:
    xs = [1, 2, 3]
    ys = [x for x in xs]

assert G2.ys == [1, 2, 3]


# literal condition without name lookups
class G3:
    xs = [1, 2, 3]
    ys = [x for x in xs if x > 2]

assert G3.ys == [3]


# module-level comprehension sees globals normally
gxs = [10, 20]
gys = [v for v in gxs if v > len(gxs)]
assert gys == [20]


# method default values are evaluated in the class body (legal)
class G4:
    xs = [1]

    def m(self, a=xs):
        return a

assert G4().m() == [1]

print("test_class_scope_comprehension_regression passed")
