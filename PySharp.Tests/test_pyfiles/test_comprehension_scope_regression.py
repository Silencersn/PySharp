# Regression: list/set/dict comprehensions have their own implicit scope
# (PEP 709 inlined, like CPython) — iteration variables never leak into
# the enclosing scope, never appear out of thin air, and never rebind
# same-named locals or closure cells. Walrus targets inside a
# comprehension bind in the enclosing scope (PEP 572), and eval/exec
# inside a comprehension sees both the comprehension's targets and the
# enclosing function's locals. All pinned values are CPython 3.14 truth.

# --- the issue's faces ---
def f():
    v = "in-f"
    _ = [v for v in range(3)]
    return v
assert f() == "in-f"

def g():
    a, b, c, d = 'a', 'b', 'c', 'd'
    _ = [a for a in range(3)]
    _ = {b: 1 for b in range(3)}
    _ = list(c for c in range(3))
    _ = {d for d in range(3)}
    return a, b, c, d
assert g() == ('a', 'b', 'c', 'd')

# --- no thin-air creation ---
def h():
    _ = [zz for zz in range(3)]
    _ = {zc: 1 for zc in range(3)}
    _ = {zs for zs in range(3)}
    try:
        zz
        return "visible"
    except NameError:
        return "NameError"
assert h() == "NameError"

# --- closure cells are not clobbered by the iteration variable ---
def n():
    w = "outer-w"
    def inner():
        return w
    _ = [w for w in range(3)]
    return inner()
assert n() == "outer-w"

# --- module scope is untouched ---
mi = "outer"
_ml = [mi for mi in range(3)]
assert mi == "outer" and _ml == [0, 1, 2]

# --- genexp scope (real nested function) unchanged ---
def k():
    _ = list((hg for hg in range(2)))
    try:
        return hg
    except NameError:
        return "NameError"
assert k() == "NameError"

# --- PEP 572: walrus in a comprehension binds in the enclosing scope ---
def walrus_fn():
    result = [y := i * 2 for i in range(3)]
    return result, y
assert walrus_fn() == ([0, 2, 4], 4)

def walrus_cell():
    acc = 0
    def inner():
        nonlocal acc
        result = [acc := i for i in range(3)]
        return result, acc
    return inner()
assert walrus_cell() == ([0, 1, 2], 2)

_mw = [mw := 7 for _ in range(1)]
assert mw == 7 and _mw == [7]

# --- nested comprehensions read the outer iteration variable ---
def nested():
    base = 10
    return [[b for b in str(base)] for _ in range(1)]
assert nested() == [['1', '0']]

# --- lambdas capture their own defaults, not the iteration variable ---
def lambdas():
    v = [1, 2]
    fns = [lambda v=v: v for v in v]
    return [f() for f in fns]
assert lambdas() == [1, 2]

# --- reading an enclosing local keeps its value and stays visible ---
def ref_outer():
    base = 10
    r = [base + x for x in range(3)]
    return r, base
assert ref_outer() == ([10, 11, 12], 10)

# --- a global shadowed by the target keeps the global intact ---
gs = "G"
def shadow_global():
    return [gs for gs in range(3)]
assert shadow_global() == [0, 1, 2]
assert gs == "G"

# --- class-body comprehensions keep their semantics ---
class C1:
    rows = [(1, 2), (3, 4)]
    flat = [r for row in rows for r in row]
assert C1.flat == [1, 2, 3, 4]

class C2:
    pairs = [(1, 2)]
    inner = [[p for p in row] for row in pairs]
assert C2.inner == [[1, 2]]

class C3:
    vals = [1, 2]
    fns = [lambda v=v: v for v in vals]
assert [f() for f in C3.fns] == [1, 2]

# --- eval inside a comprehension sees targets and enclosing locals ---
def comp_iter_var():
    return [eval("k3") for k3 in ["iter-k3"]][0]
assert comp_iter_var() == "iter-k3"

def comp_outer_local():
    kk = "outer"
    return [eval("kk") for _ in [0]][0]
assert comp_outer_local() == "outer"

# --- async comprehensions inside async functions (raw coroutine driver) ---
async def _agen():
    for i in range(3):
        yield i

async def _af():
    return [x async for x in _agen()]

async def _adf():
    return {x: x * 2 async for x in _agen()}

_c = _af()
try:
    _c.send(None)
except StopIteration as e:
    assert e.value == [0, 1, 2]
_c = _adf()
try:
    _c.send(None)
except StopIteration as e:
    assert e.value == {0: 0, 1: 2, 2: 4}

# --- compile-time rejects, verified through exec ---
try:
    exec("def sync_fn():\n    return [x async for x in [1]]")
    raise AssertionError("expected SyntaxError for async comp in sync function")
except SyntaxError:
    pass

try:
    exec("[x for x in range(3) if (x := 2)]")
    raise AssertionError("expected SyntaxError for walrus rebinding iter var")
except SyntaxError:
    pass

print("test_comprehension_scope_regression passed")
