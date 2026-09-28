"""Binding the reserved name __debug__ is a compile-time SyntaxError in every form that binds it.

CPython treats __debug__ as a write-protected name: symtable.c check_name rejects
Store with "cannot assign to __debug__" and Del with "cannot delete __debug__",
reached from four call sites — the name-definition path (assignments, parameters,
imports, def/class/type names, except-as, match captures), the attribute path
(whose context decides: writes are rejected, reads are ordinary loads), call and
class-definition keywords, and class-pattern keywords. It is not a keyword and
not in the 35-keyword whitelist, so it lexes as a NAME in every position.

The load side is deliberately the opposite: ast_preprocess.c folds a Load-context
Name into the constant `not optimize` before the symbol table is even built, so
reading __debug__ is legal everywhere and never binds a name. The guards at the
bottom pin that side down, including that it is not a scope binding.

:kind: test
:background: ast_preprocess.c folds a Load-context Name into the constant
    `not optimize` before the symbol table is built, while symtable.c
    check_name rejects Store and Del. Reading __debug__ is therefore legal
    everywhere; only binding it is rejected.
"""


def expect_syntax_error(src, message):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert e.msg == message, repr(src) + " -> " + repr(e.msg)


ASSIGN = "cannot assign to __debug__"
DELETE = "cannot delete __debug__"


def expect_assign(src):
    expect_syntax_error(src, ASSIGN)


def expect_delete(src):
    expect_syntax_error(src, DELETE)


# --- simple statement targets ---

expect_assign("__debug__ = 1")
expect_assign("x = __debug__ = 1")
expect_assign("__debug__: int = 1")
expect_assign("__debug__: int")
expect_assign("(__debug__): int = 1")
expect_assign("__debug__ += 1")
expect_assign("__debug__ -= 1")

# --- compound statement targets ---

expect_assign("for __debug__ in []:\n    pass")
expect_assign("async def f():\n    async for __debug__ in []:\n        pass")
expect_assign("with open('x') as __debug__:\n    pass")
expect_assign("try:\n    pass\nexcept TypeError as __debug__:\n    pass")

# --- tuple, list and starred targets ---

expect_assign("(__debug__, x) = (1, 2)")
expect_assign("(a, __debug__, c) = (1, 2, 3)")
expect_assign("(a, *__debug__, c) = (1, 2, 3)")
expect_assign("[__debug__] = [1]")

# --- parameter forms ---

expect_assign("def f(__debug__): pass")
expect_assign("def f(__debug__, /): pass")
expect_assign("def f(a, __debug__): pass")
expect_assign("def f(*__debug__): pass")
expect_assign("def f(**__debug__): pass")
expect_assign("def f(*, __debug__): pass")
expect_assign("def f(*xx, __debug__): pass")
expect_assign("f = lambda __debug__: 1")
expect_assign("def f(x=1, *, y=lambda __debug__: 0): pass")
expect_assign("async def f(__debug__): pass")

# a reserved name outranks the duplicate-argument check, as in CPython, where
# check_name runs per parameter before the duplicate pass
expect_assign("def f(__debug__, __debug__): pass")

# --- definition names ---

expect_assign("def __debug__(): pass")
expect_assign("async def __debug__(): pass")
expect_assign("class __debug__: pass")
expect_assign("class A:\n    class __debug__: pass")
expect_assign("def f():\n    def __debug__(): pass")
expect_assign("type __debug__ = int")

# --- import aliases ---

expect_assign("import __debug__")
expect_assign("import os as __debug__")
expect_assign("import a.b.c as __debug__")
expect_assign("from a import __debug__")
expect_assign("from a import b as __debug__")

# --- PEP 695 type parameters ---

expect_assign("def f[__debug__](): pass")
expect_assign("class C[__debug__]: pass")
expect_assign("async def f[__debug__](): pass")

# --- attribute targets: the attribute context decides ---

expect_assign("obj.__debug__ = 1")
expect_assign("obj.__debug__: int = 1")
expect_assign("obj.__debug__ += 1")
expect_assign("for obj.__debug__ in []:\n    pass")
expect_delete("del obj.__debug__")

# --- call and class-definition keywords ---

expect_assign("f(__debug__=1)")
expect_assign("class C(__debug__=42): pass")
expect_assign("class C(metaclass=type, __debug__=42): pass")

# --- match statements ---

expect_assign("match x:\n    case __debug__: pass")
expect_assign("match x:\n    case a, __debug__, b: pass")
expect_assign("match x:\n    case a, b, *__debug__: pass")
expect_assign("match x:\n    case {'k': __debug__}: pass")
expect_assign("match x:\n    case {'k': 1, **__debug__}: pass")
expect_assign("match x:\n    case C(__debug__=1): pass")

# --- deletion ---

expect_delete("del __debug__")
expect_delete("del a, __debug__")

# --- walrus ---

expect_assign("(__debug__ := 1)")
expect_assign("if (__debug__ := 1):\n    pass")
expect_assign("[(__debug__ := 1) for _ in []]")

# --- the load side is legal everywhere, and does not bind a name ---

# reading folds to the optimization-level constant
assert __debug__ is True
assert __debug__ is bool(__debug__)
assert __debug__ is __debug__

# a load inside a function reads the constant, not a parameter/local
def read_in_function():
    return __debug__


assert read_in_function() is True

# an attribute read is an ordinary load, not a binding
class Holder:
    pass


try:
    Holder.__debug__
except AttributeError:
    pass  # the attribute does not exist; the point is that it is not a SyntaxError

# `global __debug__` is accepted: DEF_GLOBAL is outside check_name's write mask
def declare_global():
    global __debug__
    return __debug__


assert declare_global() is True

# a load must not register the name in the enclosing scope
def load_does_not_bind():
    value = __debug__
    return "__debug__" in locals(), value


assert load_does_not_bind() == (False, True)

# exec and eval compile the load the same way
namespace = {}
exec("result = __debug__", namespace)
assert namespace["result"] is True
assert eval("__debug__") is True

# legal writes to neighbouring names still compile
compile("_debug__ = 1", "<test>", "exec")
compile("__debug = 1", "<test>", "exec")
compile("debug = 1", "<test>", "exec")

print("test_debug_reserved_name passed")
