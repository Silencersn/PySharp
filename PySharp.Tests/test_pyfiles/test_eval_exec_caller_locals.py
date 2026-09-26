"""eval()/exec() with default namespaces use the calling frame's namespaces: a discarded snapshot of its fast locals for optimized frames, the live mapping for unoptimized ones (class bodies).

Covers local shadowing and NameError fallthrough, exec/walrus writes staying out of globals, explicit global declarations, cell and free vars, generator and coroutine frames, comprehensions, and code-object paths.

:kind: test
"""
# Regression test: eval/exec with default globals/locals must use the
# CALLING frame's namespaces (CPython builtin_eval/exec_impl "fromframe"):
# a snapshot of the caller's fast locals for optimized frames (cell and
# free vars dereferenced, unbound slots hidden, writes discarded), the
# live mapping for unoptimized ones (class namespace).
#
# PySharp used to build only the globals side, so a function-local name
# was invisible to eval() — silently shadowed by a same-named global or a
# plain NameError — and exec()/walrus writes leaked into module globals.
#
# An explicit `global` declaration in exec'd code must still target the
# real globals (CPython emits STORE_GLOBAL for it even at module scope).

v = "global-v"


# --- the reported repro: local shadows global ---
def shadow():
    v = "closure-v"
    return eval("v")

assert shadow() == "closure-v"


# --- local without a same-named global used to raise NameError ---
def plain_local():
    local = 5
    return eval("local + 1")

assert plain_local() == 6


# --- explicit-parameter paths are unaffected ---
assert eval("x + 1", {"x": 5}) == 6
assert eval("y + 1", {}, {"y": 9}) == 10


# --- globals given: the caller's locals stay invisible ---
def hidden_local():
    hidden = 1
    try:
        eval("hidden", {"x": 5})
        return "visible"
    except NameError:
        return "NameError"

assert hidden_local() == "NameError"


# --- globals omitted, locals given: caller globals still visible ---
assert eval("v + suffix", None, {"suffix": "!"}) == "global-v!"


# --- exec writes land in the discarded snapshot, not globals ---
def exec_writes():
    a = 1
    exec("a = 2; b = 3")
    return (a, "b" in globals())

assert exec_writes() == (1, False)


# --- walrus inside eval writes the snapshot too ---
def eval_walrus():
    r = eval("(w := 4)")
    try:
        w
        vis = "visible"
    except NameError:
        vis = "NameError"
    return (r, vis, "w" in globals())

assert eval_walrus() == (4, "NameError", False)


# --- explicit global declaration in exec'd code hits the real globals ---
g1 = []
def exec_global_decl():
    exec("global gg\ngg = 9")
    exec("global gd\ndel_gd = 1")
    return globals().get("gg")

gd = 1
def exec_global_del():
    exec("global gd\ndel gd")
    return "gd" in globals()

assert exec_global_decl() == 9
assert exec_global_del() is False


# --- non-declared store in exec stays local to the snapshot ---
snap = {}
def exec_snap_load():
    exec("res = gg + 1")
    return globals().get("res")

assert exec_snap_load() is None


# --- parameters and locals shadow builtins ---
def param(p):
    return eval("p * 2")

assert param(21) == 42

def shadows_builtin():
    len = 5
    return eval("len")

assert shadows_builtin() == 5


# --- reading before the local is bound falls through to NameError ---
def unbound():
    try:
        return eval("a")
    except NameError:
        return "NameError"
    a = 1

assert unbound() == "NameError"


# --- deleted local falls back to the global ---
def deleted():
    v = "local-then-deleted"
    del v
    return eval("v")

assert deleted() == "global-v"


# --- cell vars of the caller are visible (dereferenced) ---
def cell_owner():
    cellvar = "cc"
    def inner():
        return cellvar
    return eval("cellvar")

assert cell_owner() == "cc"


# --- a free var is visible only when the caller's code references it ---
def free_owner():
    v = "cv"
    def g():
        _ = v          # makes v a real free variable of g
        return eval("v")
    return g()

def not_free():
    v = "cv2"
    def g():
        return eval("v")   # v never appears in g's own code
    return g()

assert free_owner() == "cv"
assert not_free() == "global-v"


# --- nested eval sees the inner eval frame's locals ---
def nested():
    v = "nested"
    return eval("eval('v')")

assert nested() == "nested"


# --- generators and coroutines use their own frame locals ---
def gen_frames():
    def gen():
        x = "gen-x"
        yield eval("x")
        x = "gen-x2"
        yield eval("x")
    return list(gen())

assert gen_frames() == ["gen-x", "gen-x2"]

async def coro():
    local = "async-v"
    return eval("local")

_c = coro()
try:
    _c.send(None)
except StopIteration as e:
    _async_result = e.value

assert _async_result == "async-v"


# --- class body: the live class namespace (writes persist, CPython m7) ---
class Live:
    x = 1
    r1 = eval("x")
    exec("y = 2")
    r2 = "y" in locals()

assert (Live.r1, Live.r2, hasattr(Live, "y")) == (1, True, True)


# --- class body inside a function: class ns first, then globals ---
def class_in_func():
    fv = "func-v"
    class Inner:
        cv = "class-v"
        r1 = eval("cv")
        r2 = eval("v")
        try:
            r3 = eval("fv")
        except NameError:
            r3 = "NameError"
    return (Inner.r1, Inner.r2, Inner.r3)

assert class_in_func() == ("class-v", "global-v", "NameError")


# --- function-parented comprehension: the PEP 709 owner frame owns the
# target slot, so eval inside sees it (inline frames run fast-local ops
# against the owner's span) ---
def comp_iter_var():
    return [eval("k3") for k3 in ["iter-k3"]][0]

assert comp_iter_var() == "iter-k3"

def comp_outer_local():
    k = "outer"
    return [eval("k") for _ in [0]][0]

assert comp_outer_local() == "outer"


# --- module-level comprehension: target resolved through the frame's
# globals copy ---
assert [eval("k2") for k2 in ["iter-k"]][0] == "iter-k"


# --- class-body comprehension: eval sees the target (the comprehension's
# own name) but NOT the class namespace (class scope is invisible to it) ---
class CompInClass:
    x = 1
    try:
        leaked = [eval("x") for _ in [0]][0]
    except NameError:
        leaked = "NameError"

assert CompInClass.leaked == "NameError"

class CompTargetInClass:
    r = [eval("k2") for k2 in ["t"]][0]

assert CompTargetInClass.r == "t"


# --- code-object paths resolve the same defaults ---
def code_obj_paths():
    q = 41
    r = eval(compile("q + 1", "<c>", "eval"))
    exec(compile("zz = 7", "<c>", "exec"))
    return (r, "zz" in locals(), "zz" in globals())

assert code_obj_paths() == (42, False, False)


# --- __builtins__/__name__ still resolve through globals ---
assert eval("'__name__' in globals()")
assert eval("__builtins__ is not None")


# --- try/except inside exec'd code with default locals ---
def exec_exc():
    try:
        exec("raise ValueError('boom')")
    except ValueError as e:
        return ("caught", str(e))

assert exec_exc() == ("caught", "boom")


print("test_eval_exec_caller_locals passed")
