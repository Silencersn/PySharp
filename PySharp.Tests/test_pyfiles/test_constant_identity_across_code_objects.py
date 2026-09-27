"""Equal constants in different code objects of one compilation are one object.

CPython's const cache (c_const_cache) belongs to the compilation, not to a code
object: the module body, every function/lambda/class/generator body it nests,
and the elements of a constant tuple all resolve an equal constant to the same
object. So a literal written in a function body is the very object the module
body holds, and a bare constant is the object nested inside a constant tuple.

The strings here are deliberately non-identifier ("a b" rather than "ab"): an
identifier-shaped string literal is interned process-wide by CPython, which
would make `is` succeed for a reason unrelated to the const cache. Two separate
compilations (two exec calls) each start a fresh cache, so they do NOT share.

Type stays part of a constant's identity, so 1, True, 1.0, 1+0j, b"a" and "a"
remain six distinct constants even across code objects.

:kind: test
:background: CPython keys a compile unit's constants by _PyCode_ConstantKey
    (Objects/codeobject.c) inside a cache built per compilation
    (Python/compile.c compiler_setup, c_const_cache), merged recursively by
    merge_consts_recursive, which also rewrites a constant tuple's elements to
    the canonical objects. Two compile() calls do not share: the cache is
    per-compilation, not global (Lib/codeop.py shows the same boundary).
"""


# --- function body vs module body, over several constant kinds ---

MOD_STR = "a b"
MOD_TUPLE = (1, 2)
MOD_FLOAT = 3.5
MOD_BYTES = b"a b"
MOD_BIGINT = 10 ** 30


def fn_str():
    return "a b"


def fn_tuple():
    return (1, 2)


def fn_float():
    return 3.5


def fn_bytes():
    return b"a b"


def fn_bigint():
    return 10 ** 30


assert fn_str() is MOD_STR
assert fn_tuple() is MOD_TUPLE
assert fn_float() is MOD_FLOAT
assert fn_bytes() is MOD_BYTES
assert fn_bigint() is MOD_BIGINT


# --- two function bodies agree with each other ---


def fn_str_again():
    return "a b"


assert fn_str() is fn_str_again()
assert fn_str_again() is MOD_STR


# --- lambda, generator, class and comprehension bodies ---

lam = lambda: "a b"
assert lam() is MOD_STR


def gen_body():
    yield "a b"


assert next(gen_body()) is MOD_STR


class Holder:
    value = "a b"
    pair = (1, 2)


assert Holder.value is MOD_STR
assert Holder.pair is MOD_TUPLE

# A generator expression body is its own code object; a list/set/dict
# comprehension is inlined into the enclosing one. Both must agree.
assert next(("a b") for _ in range(1)) is MOD_STR
assert [("a b") for _ in range(1)][0] is MOD_STR
assert list({("a b") for _ in range(1)})[0] is MOD_STR
assert list({("a b"): 1 for _ in range(1)})[0] is MOD_STR


# --- a nested function body agrees with the module body ---


def outer():
    def inner():
        return "a b"

    return inner()


assert outer() is MOD_STR


# --- a constant tuple's elements are canonical objects too ---

assert MOD_STR is ("a b",)[0]
assert MOD_STR is (("a b",),)[0][0]
assert MOD_BIGINT is (10 ** 30,)[0]
assert MOD_FLOAT is (3.5,)[0]
assert MOD_BYTES is (b"a b",)[0]


def tuple_holder():
    return ("a b",)


def tuple_holder_again():
    return ("a b",)


assert tuple_holder()[0] is tuple_holder_again()[0]
assert tuple_holder()[0] is MOD_STR


# --- identity is type-sensitive across code objects ---


def t_int():
    return (1,)


def t_bool():
    return (True,)


def t_float():
    return (1.0,)


def t_complex():
    return (1 + 0j,)


def t_bytes():
    return (b"a",)


def t_str():
    return ("a",)


def t_zero():
    return (0.0,)


def t_neg_zero():
    return (-0.0,)


assert not (t_int() is t_bool())
assert not (t_int() is t_float())
assert not (t_int() is t_complex())
assert not (t_bytes() is t_str())
assert not (t_zero() is t_neg_zero())
assert t_int() is t_int()
assert t_neg_zero() is t_neg_zero()

# Bare constants keep the same distinction against a tuple element.
assert not (1 is (True,)[0])
assert not (b"a" is ("a",)[0])
assert not (0.0 is (-0.0,)[0])


# --- two separate compilations do not share ---

_first = {}
_second = {}
exec("v = 'a b'", _first)
exec("v = 'a b'", _second)
assert not (_first["v"] is _second["v"])

_tuple_first = {}
_tuple_second = {}
exec("v = (1, 2)", _tuple_first)
exec("v = (1, 2)", _tuple_second)
assert not (_tuple_first["v"] is _tuple_second["v"])


# --- value semantics are untouched ---

assert fn_str() == MOD_STR
assert fn_tuple() == MOD_TUPLE
assert (1, 2) == (1, 2)
assert [x for x in (1, 2)] == [1, 2]
assert {"a b": 1}["a b"] == 1


print("constant identity across code objects ok")
