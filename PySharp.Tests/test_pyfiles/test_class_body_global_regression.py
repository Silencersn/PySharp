"""
Regression: `global` declarations inside a class body route bindings to the
module globals instead of the class namespace.

CPython's symtable marks a class-body name with an explicit `global`
declaration GLOBAL_EXPLICIT, so every binding form in the class body —
plain and augmented assignment, walrus targets, for/with/except-as
targets, import-as, def and class statements — compiles to
STORE_GLOBAL/LOAD_GLOBAL/DELETE_GLOBAL and never touches the class dict.
Except-as cleanup deletes the module global at block exit. Class scopes
stay invisible to nested functions: a method resolves the name globally,
and an enclosing function's local still wins over the class declaration.

An annotated name cannot be combined with a global/nonlocal declaration
in any order in function and class scopes, or after the annotation at
module level ("annotated name 'X' can't be global" / "... can't be
nonlocal"); a module-level `global X` before the annotation stays legal.
"""

# --- the reported case: the binding lands in the module globals ---
class E:
    global GG
    GG = 5

assert GG == 5, GG
assert not hasattr(E, "GG")
assert "GG" not in vars(E)

# --- rebinding an existing module global takes effect ---
REBIND = 1
class Rebinder:
    global REBIND
    REBIND = 2
assert REBIND == 2 and not hasattr(Rebinder, "REBIND")

# --- loads in the class body see the module value before and after ---
LOAD_TARGET = "mod"
class Loader:
    global LOAD_TARGET
    before = LOAD_TARGET
    LOAD_TARGET = "class-set"
    after = LOAD_TARGET
assert Loader.before == "mod"
assert Loader.after == "class-set"
assert LOAD_TARGET == "class-set"
assert not hasattr(Loader, "LOAD_TARGET")

# --- augmented assignment ---
AUG = 10
class Aug:
    global AUG
    AUG += 5
assert AUG == 15 and not hasattr(Aug, "AUG")

# --- walrus target ---
class W:
    global WG
    v = (WG := 7)
assert WG == 7 and W.v == 7 and not hasattr(W, "WG")

# --- del ---
DELV = 1
class D:
    global DELV
    del DELV
assert "DELV" not in globals() and not hasattr(D, "DELV")

# --- import-as / def / class statements bind at module level ---
class Binder:
    global math_mod, gfn, gcls
    import math as math_mod
    def gfn():
        return "fn"
    class gcls:
        pass
import math
assert math_mod is math and not hasattr(Binder, "math_mod")
assert gfn() == "fn" and not hasattr(Binder, "gfn")
assert gcls.__name__ == "gcls" and not hasattr(Binder, "gcls")

# --- for / with targets honor the declaration ---
class Ctx:
    def __enter__(self):
        return "ctx"
    def __exit__(self, *exc):
        return False

class FlowBinder:
    global FORV, WITHV
    for FORV in (10, 20):
        pass
    with Ctx() as WITHV:
        pass
assert FORV == 20 and not hasattr(FlowBinder, "FORV")
assert WITHV == "ctx" and not hasattr(FlowBinder, "WITHV")

# --- except-as stores the module global; the cleanup deletes it ---
class Exc:
    global EXCV
    try:
        raise ValueError("x")
    except ValueError as EXCV:
        seen = isinstance(EXCV, ValueError)
assert Exc.seen
assert "EXCV" not in globals() and not hasattr(Exc, "EXCV")

# --- generic class bodies follow the same routing ---
class GC[T]:
    global GCG
    GCG = 3
assert GCG == 3 and not hasattr(GC, "GCG")

# --- method bodies read the module global ---
class M:
    global MG
    MG = 5
    def read(self):
        return MG
assert MG == 5 and M().read() == 5

# --- an enclosing function local wins over the class's declaration ---
X = "unset"
def outer():
    X = "fn-local"
    class C:
        global X
        X = "global-set"
        def m(self):
            return X
    return C().m(), X
m_result, outer_x = outer()
assert m_result == "fn-local"
assert X == "global-set"
assert outer_x == "fn-local"

# --- plain class attributes (no global declaration) are unaffected ---
class Plain:
    P = 1
    other = P
    (PW := 2)
assert Plain.P == 1 and Plain.other == 1
assert Plain.PW == 2
assert "P" in vars(Plain)

# --- function-scope global declarations keep working ---
def fn():
    global FG
    FG = 20
fn()
assert FG == 20

# --- class-body nonlocal still rebinds the enclosing function local ---
def nonlocal_outer():
    nx = 1
    class NC:
        nonlocal nx
        nx = 2
    return nx
assert nonlocal_outer() == 2

# --- guards: annotation combined with global/nonlocal is a compile error ---
def expect_error(src, message):
    try:
        compile(src, "<t>", "exec")
    except SyntaxError as e:
        assert message in str(e), str(e)
    else:
        raise AssertionError("expected SyntaxError: " + src)

expect_error(
    "class A:\n    global AG\n    AG: int = 9\n",
    "annotated name 'AG' can't be global")
expect_error(
    "class A:\n    AG: int = 9\n    global AG\n",
    "annotated name 'AG' can't be global")
expect_error(
    "def f():\n    global FG2\n    FG2: int = 9\n",
    "annotated name 'FG2' can't be global")
expect_error(
    "def f():\n    FG2: int = 9\n    global FG2\n",
    "annotated name 'FG2' can't be global")
expect_error(
    "X: int = 1\nglobal X\n",
    "annotated name 'X' can't be global")
expect_error(
    "def o():\n    nx = 1\n    def i():\n        nonlocal nx\n        nx: int = 2\n",
    "annotated name 'nx' can't be nonlocal")
expect_error(
    "def o():\n    nx = 1\n    def i():\n        nx: int = 2\n        nonlocal nx\n",
    "annotated name 'nx' can't be nonlocal")

# a module-level `global X` before the annotation stays legal
compile("global ZV\nZV: int = 5\n", "<t>", "exec")

# --- guards: use/assignment before the declaration is still refused ---
expect_error(
    "class C:\n    y = X\n    global X\n",
    "name 'X' is used prior to global declaration")
expect_error(
    "class C:\n    X = 1\n    global X\n",
    "name 'X' is assigned to before global declaration")

print("class body global regression passed")
