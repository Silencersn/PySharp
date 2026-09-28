"""
An exception holding itself in args recurses in BaseException_str/repr
entirely in native code: str(e) re-enters str() on args[0], which is the
exception again, and no Python frame is entered along the way, so the frame
counters could not bound it. The process died with an uncatchable
StackOverflowException instead of raising, and even repr([e]) (which does
enter the container guard) crashed, because the exception's own str/repr
slots sat outside every guarded entry.

CPython 3.14 bounds this in PyObject_Str/PyObject_Repr (Objects/object.c),
which enter _Py_EnterRecursiveCall before dispatching to tp_str/tp_repr
— BaseException_str/repr carry no guard of their own, they are protected
by the callers. The RecursionError is therefore catchable and the
interpreter survives; note CPython renders a cycle in a *container* as
[...] but has no such placeholder for a self-referential exception, so
the expected outcome here is a raised RecursionError, never a placeholder.

PySharp now probes the native stack at the same two entries, so every
self-referential exception shape below raises instead of killing the
process, and plain exception rendering is unaffected.

:kind: test
"""


def expect_recursion(fn):
    try:
        fn()
    except RecursionError as exc:
        assert str(exc)
        return
    raise AssertionError("RecursionError not raised")


# ------------------------------------------------- args self-reference
def self_ref_args():
    e = ValueError()
    e.args = (e,)
    return e


expect_recursion(lambda: str(self_ref_args()))
expect_recursion(lambda: repr(self_ref_args()))
expect_recursion(lambda: print(self_ref_args()))
expect_recursion(lambda: f"{self_ref_args()}")
expect_recursion(lambda: format(self_ref_args(), ""))
expect_recursion(lambda: "%s" % self_ref_args())

# the container guard covers repr([e]), but the exception's own str/repr is
# reached from inside it, so this crashed too
e = self_ref_args()
expect_recursion(lambda: repr([e]))
expect_recursion(lambda: repr((e,)))
expect_recursion(lambda: repr({"k": e}))


# --------------------------------------------------- mutually referential
def mutually_ref():
    a = ValueError()
    b = TypeError()
    a.args = (b,)
    b.args = (a,)
    return a, b


a, b = mutually_ref()
expect_recursion(lambda: str(a))
expect_recursion(lambda: str(b))
expect_recursion(lambda: repr(a))


# ------------------------------------------- subclass slots fall through
# KeyError renders a lone argument with repr(), and SyntaxError/OSError
# render members through str(); all of them re-enter the guarded entries
k = KeyError("k")
k.args = (k,)
expect_recursion(lambda: str(k))
expect_recursion(lambda: repr(k))

s = SyntaxError("m")
s.msg = s
expect_recursion(lambda: str(s))

o = OSError(1, "x")
o.strerror = o
expect_recursion(lambda: str(o))


# ------------------------------------------------ shallow results unchanged
assert str(ValueError()) == ""
assert str(ValueError("x")) == "x"
assert repr(ValueError("x")) == "ValueError('x')"
assert str(ValueError("a", "b")) == "('a', 'b')"
assert repr(ValueError("a", "b")) == "ValueError('a', 'b')"
assert str(KeyError("k")) == "'k'"
assert repr(KeyError("k")) == "KeyError('k')"
assert str(IndexError("i")) == "i"

# an exception whose args merely contain an unrelated exception is fine
inner = ValueError("inner")
outer = ValueError(inner)
assert str(outer) == "inner"
assert repr(outer) == "ValueError(ValueError('inner'))"

# the interpreter survives every caught recursion above
assert str(ValueError("after")) == "after"
print("ok")
