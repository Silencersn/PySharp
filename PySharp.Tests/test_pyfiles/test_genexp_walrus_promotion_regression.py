# Regression: PEP 572 walrus targets inside a generator expression bind in the
# nearest enclosing non-comprehension scope (CPython
# symtable_extend_namedexpr_scope), never in the genexp scope itself.
# Function-level targets bind through a shared closure cell; module-level
# targets bind globally.

# basic promotion: target readable after the genexp is consumed
def f6():
    g = (g6 := i * 11 for i in range(2))
    list(g)
    return g6
assert f6() == 11

def f7():
    g = (w7 := i + 1 for i in range(2))
    list(g)
    return w7
assert f7() == 2

# a pre-assigned target keeps its value until the genexp stores
def f_pre():
    g6 = 0
    g = (g6 := i * 11 for i in range(2))
    list(g)
    return g6
assert f_pre() == 11

# reading the target before assignment is an UnboundLocalError on the
# owning frame (the target makes the outer name local), and the error
# names the variable, not a slot index
def f_unbound():
    try:
        print(g1)
    except UnboundLocalError as e:
        assert "local variable 'g1'" in str(e)
    else:
        raise AssertionError("expected UnboundLocalError")
    list(g := (g1 := 1 for _ in [1]))
    return g1
assert f_unbound() == 1

# module-level target binds globally
list(gm := (m1 := 41 for _ in [1]))
assert m1 == 41

# target reused inside the same genexp after the store
def f_reuse():
    g = ((t := i, t + 1) for i in range(2))
    result = list(g)
    assert result == [(0, 1), (1, 2)]
    return t
assert f_reuse() == 1

# filter idiom works the same in genexps and listcomps
def f_filter():
    from_gen = list(x for x in range(6) if (y := x * 2) > 4)
    assert from_gen == [3, 4, 5]
    assert y == 10
f_filter()

# rebinding an outer parameter through the genexp
def f_param(a):
    list(g := (a := a + 1 for _ in [1]))
    return a
assert f_param(10) == 11

# a global declaration in the owner makes the genexp store the global
def f_global():
    global gw
    list(g := (gw := 7 for _ in [1]))
f_global()
assert gw == 7

# nested genexp inside a genexp: target binds to the function
def f_nested():
    g = ((n8 := j) for j in (k for k in range(2)))
    list(g)
    return n8
assert f_nested() == 1

# a lambda created in the genexp sees the target through the shared cell
def f_lambda():
    g = ((c9 := 5, (lambda: c9)) for _ in [1])
    val, fn = next(iter(g))
    assert val == 5
    return fn()
assert f_lambda() == 5

# lazy consumption observes intermediate stores
def f_lazy():
    g = (z10 := i for i in range(3))
    next(g)
    mid = z10
    list(g)
    return mid, z10
assert f_lazy() == (0, 2)

# listcomp walrus behavior is unchanged
def f_listcomp():
    out = [(v11 := i * 3) for i in range(2)]
    assert out == [0, 3]
    return v11
assert f_listcomp() == 3

# empty-cell errors match CPython: the owning frame raises
# UnboundLocalError (local variable wording), a consumer frame raises
# NameError (free variable wording)
def cell_owner():
    def inner():
        return x
    try:
        print(x)
    except UnboundLocalError as e:
        assert "local variable 'x'" in str(e)
    else:
        raise AssertionError("expected UnboundLocalError")
    x = 1
cell_owner()

def cell_consumer():
    def inner():
        return x
    try:
        inner()
    except NameError as e:
        assert "free variable 'x'" in str(e)
    else:
        raise AssertionError("expected NameError")
    x = 1
cell_consumer()

# walrus syntax restrictions still match CPython
def assert_syntax_error(src, fragment):
    try:
        compile(src, "<test>", "exec")
    except SyntaxError as e:
        assert fragment in str(e)
    else:
        raise AssertionError("expected SyntaxError")

assert_syntax_error(
    "class C:\n    g = (x := 1 for _ in [2])\n",
    "class body")
assert_syntax_error(
    "g = (i := i for i in range(2))\n",
    "rebind comprehension iteration variable")
assert_syntax_error(
    "g = (i for i in (i := range(2)))\n",
    "comprehension iterable expression")

print("test_genexp_walrus_promotion_regression passed")
