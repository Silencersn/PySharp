"""
Regression: container repr and tuple hashing recurse in native code only,
so the Python frame counters could not bound them. A deeply nested list (or
dict/tuple/frozenset) killed the process with an uncatchable
StackOverflowException during repr/str/print/f-string/format, and a deeply
nested tuple did the same while being hashed — including when it was merely
inserted as a dict key or into a set. Both entries now probe the native
stack and raise a catchable RecursionError, like the container comparison
entry already does (see test_container_comparison_recursion_regression.py).

CPython 3.14 reference: PyObject_Repr enters _Py_EnterRecursiveCall, which
compares machine stack usage rather than counting calls. tuplehash reaches
the element hashes through PyObject_Hash with no catchable guard at all, so
CPython hashes about 45000 levels and then dies outright (measured on
3.14.4: ok at 45000, process death at 50000) — the hash cases below can
therefore only assert that the call does not take the process down.
"""


def expect_recursion(fn):
    try:
        fn()
    except RecursionError as exc:
        assert str(exc)
        return
    raise AssertionError("RecursionError not raised")


def allow_recursion_or_completion(fn):
    try:
        fn()
    except RecursionError as exc:
        assert str(exc)


# ------------------------------------------------------------ cycles
def cyc_list():
    x = [1]
    x.append(x)
    return x


def cyc_dict():
    y = {"k": 1}
    y["self"] = y
    return y


assert repr(cyc_list()) == "[1, [...]]"
assert repr(cyc_dict()) == "{'k': 1, 'self': {...}}"


# ------------------------------------------------------- acyclic depth
def nest_list(n):
    x = [0]
    for _ in range(n):
        x = [x]
    return x


def nest_tuple(n):
    x = (0,)
    for _ in range(n):
        x = (x,)
    return x


def nest_dict(n):
    x = {"k": 0}
    for _ in range(n):
        x = {"k": x}
    return x


def nest_frozenset(n):
    x = frozenset()
    for _ in range(n):
        x = frozenset([x])
    return x


# far beyond any plausible thread stack: CPython raises RecursionError
# around 26000 levels, PySharp at its own stack budget
deep = 50000
expect_recursion(lambda: repr(nest_list(deep)))
expect_recursion(lambda: str(nest_list(deep)))
expect_recursion(lambda: print(nest_list(deep)))
expect_recursion(lambda: f"{nest_list(deep)}")
expect_recursion(lambda: format(nest_list(deep), ""))
expect_recursion(lambda: repr(nest_tuple(deep)))
expect_recursion(lambda: repr(nest_dict(deep)))
expect_recursion(lambda: repr(nest_frozenset(deep)))


# ------------------------------------------- hashing a nested tuple
# the same structure hashed, used as a dict key and stored in a set; the
# guard bounds PySharp at its own budget, while CPython finishes — or, well
# past this depth, dies without raising
allow_recursion_or_completion(lambda: hash(nest_tuple(5000)))
allow_recursion_or_completion(lambda: {nest_tuple(5000): 1})
allow_recursion_or_completion(lambda: {nest_tuple(5000)})


# ----------------------------------------------- shallow results unchanged
assert repr([1, 2]) == "[1, 2]"
assert repr((1,)) == "(1,)"
assert repr({"a": [1, 2]}) == "{'a': [1, 2]}"
assert repr(frozenset([1])) == "frozenset({1})"
assert repr(frozenset([frozenset([1])])) == "frozenset({frozenset({1})})"
assert repr(nest_list(100)).startswith("[[[[")
assert repr(nest_tuple(100)).startswith("(((((")
assert repr(nest_dict(100)).startswith("{'k': {'k':")
assert str([1, 2]) == "[1, 2]"
assert f"{[1, 2]}" == "[1, 2]"
assert format([1, 2], "") == "[1, 2]"
assert hash(nest_tuple(100)) == hash(nest_tuple(100))
assert {nest_tuple(3): 1}[nest_tuple(3)] == 1
assert nest_tuple(3) in {nest_tuple(3)}
assert nest_list(3) == nest_list(3)

# the interpreter survives every caught recursion above
assert repr(nest_list(10)) == "[" * 11 + "0" + "]" * 11
assert {"a": [1, 2]} == {"a": [1, 2]}
print("ok")
