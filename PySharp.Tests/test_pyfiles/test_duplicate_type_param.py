"""Duplicate PEP 695 type parameters are a compile-time SyntaxError on every carrier.

CPython registers the type params of all carriers (class, def, async def, type
alias; TypeVar, TypeVarTuple and ParamSpec alike) through one symbol-table entry
point whose clash check rejects a second registration of the same name before
any code is generated — compile() never returns bytecode for it. The error
points at the repeated parameter itself, so lineno/offset land on the second
occurrence, even across lines.

A parameter sharing a type param's name is not a duplicate of it, and
same-named type params in nested scopes are independent declarations.

:kind: test
:background: symtable.c registers type params of every carrier through one
    entry point whose clash check rejects duplicates uniformly; the semantic
    analyzer had no equivalent check, so duplicate names reached codegen and
    broke the class-build closure invariant (raw host crash on class, silent
    acceptance with a degenerate __type_params__ on def).
"""


def expect_syntax_error(src, message, lineno=1, offset=None):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        assert e.msg == message, repr(src) + " -> " + repr(e.msg)
        assert e.lineno == lineno, repr(src) + " -> lineno " + repr(e.lineno)
        if offset is not None:
            assert e.offset == offset, repr(src) + " -> offset " + repr(e.offset)


DUPLICATE_T = "duplicate type parameter 'T'"

# --- every carrier rejects before any code is generated ---

expect_syntax_error("class C[T, T]: pass", DUPLICATE_T, offset=12)
expect_syntax_error("def f[T, T](): pass", DUPLICATE_T, offset=10)
expect_syntax_error("async def f[T, T](): pass", DUPLICATE_T, offset=16)
expect_syntax_error("type X[T, T] = int", DUPLICATE_T, offset=11)

# --- TypeVarTuple and ParamSpec shapes are checked the same way ---

expect_syntax_error("class C[*Ts, *Ts]: pass", "duplicate type parameter 'Ts'", offset=14)
expect_syntax_error("class C[**P, **P]: pass", "duplicate type parameter 'P'", offset=14)

# --- the location is the repeated parameter, not the header ---

expect_syntax_error("def f[T, U, T](): pass", DUPLICATE_T, offset=13)
expect_syntax_error("class C[T, U, V, T]: pass", DUPLICATE_T, offset=18)
expect_syntax_error("class C[\n    T,\n    T\n]: pass", DUPLICATE_T, lineno=3, offset=5)

# --- nested carriers are reached too ---

expect_syntax_error("class C:\n    def m[T, T](): pass", DUPLICATE_T, lineno=2, offset=14)
expect_syntax_error("def f():\n    type X[T, T] = int", DUPLICATE_T, lineno=2, offset=15)

# --- exec compiles through the same check ---

try:
    exec("class C[T, T]: pass", {})
    assert False, "exec should raise SyntaxError"
except SyntaxError as e:
    assert e.msg == DUPLICATE_T, repr(e.msg)

# --- non-duplicates still compile ---

compile("class C[T, U, V]: pass", "<test>", "exec")
compile("class C[*Ts, **P]: pass", "<test>", "exec")
compile("type X[T] = int", "<test>", "exec")
compile("def f[T, U](): pass", "<test>", "exec")

# same name in nested scopes: independent declarations
compile("class C[T]:\n    def m[T](): pass", "<test>", "exec")
compile("def f[T]():\n    def g[T](): pass", "<test>", "exec")

# nested generic bodies still work end to end
class C[T]:
    def m[U]():
        return T, U


assert C.__name__ == "C"
assert len(C.__type_params__) == 1
assert C.__type_params__[0].__name__ == "T"

def f[T](x: T) -> T:
    return x


assert f.__type_params__[0].__name__ == "T"
assert f(42) == 42

print("test_duplicate_type_param passed")
