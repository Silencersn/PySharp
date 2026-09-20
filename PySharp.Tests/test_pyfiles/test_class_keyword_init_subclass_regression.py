"""
Regression: class keywords must be consumed by an __init_subclass__ hook.

CPython type_new hands the class keywords to the first __init_subclass__
after the new class (Objects/typeobject.c type_new_init_subclass, called
from type_new_impl: super(type, type).__init_subclass__(**kwds)), and
object's default implementation is a METH_CLASS | METH_NOARGS builtin that
rejects every argument (typeobject.c object_init_subclass). PySharp's
default hook declared **kwargs and swallowed them, so a class keyword
nobody consumed -- a misspelled one included -- passed silently.

The rejection names the class the descriptor bound to
(Objects/methodobject.c meth_get__qualname__: __self__.__qualname__ + '.'
+ __name__), which for a class being created is the new class itself.

Cases whose wording still differs carry a comment with CPython's text;
they only assert the exception type.
"""


def expect_type_error(label, fn, message=None):
    try:
        fn()
    except TypeError as e:
        if message is not None:
            assert str(e) == message, label + ": " + str(e)
        print("  " + label + ": " + str(e))
        return
    raise AssertionError(label + ": expected TypeError")


class Plain:
    pass


# ---------------------------------------------------------------------------
# nobody consumes: the reported shape, and the typo it used to hide
# ---------------------------------------------------------------------------
def case_nobody_consumes():
    class Extra(Plain, mystery=1):
        pass


def case_misspelled_keyword():
    class Sub(Plain, inhert=True):
        pass


expect_type_error(
    "nobody-consumes", case_nobody_consumes,
    "case_nobody_consumes.<locals>.Extra.__init_subclass__() takes no keyword arguments")
expect_type_error(
    "misspelled-keyword", case_misspelled_keyword,
    "case_misspelled_keyword.<locals>.Sub.__init_subclass__() takes no keyword arguments")


# ---------------------------------------------------------------------------
# a base that forwards **kwargs reaches the default hook, which rejects
# CPython names the new class; PySharp reports the builtin's own qualname
# ---------------------------------------------------------------------------
class Forwarding:
    def __init_subclass__(cls, **kwargs):
        super().__init_subclass__(**kwargs)


def case_forwarding_chain():
    class SubFwd(Forwarding, extra=5):
        pass


expect_type_error("forwarding-chain", case_forwarding_chain)


# ---------------------------------------------------------------------------
# consumed keywords still work, including the nearest-base rule
# ---------------------------------------------------------------------------
class Consumer:
    def __init_subclass__(cls, **kwargs):
        cls.captured = dict(kwargs)


class ConsumerChild(Consumer):
    pass


class KeywordOnly:
    def __init_subclass__(cls, *, mode="x"):
        cls.mode = mode


class Mixed:
    def __init_subclass__(cls, tag, **kwargs):
        cls.tag = tag
        cls.rest = dict(kwargs)


def case_consumed():
    class SubC(Consumer, tag="t", n=2):
        pass

    class SubDeep(ConsumerChild, deep=1):
        pass

    class SubKO(KeywordOnly, mode="y"):
        pass

    class SubKO2(KeywordOnly):
        pass

    class SubMixed(Mixed, tag="a", extra=1):
        pass

    return (SubC.captured, SubDeep.captured, SubKO.mode, SubKO2.mode,
            SubMixed.tag, SubMixed.rest)


assert case_consumed() == (
    {"tag": "t", "n": 2}, {"deep": 1}, "y", "x", "a", {"extra": 1}
), case_consumed()


# ---------------------------------------------------------------------------
# the other entry points: type(...) with keywords, and explicit super() calls
# ---------------------------------------------------------------------------
def case_type_three_args():
    type("X", (Plain,), {}, foo=1)


def case_type_three_args_consumed():
    return type("X2", (Consumer,), {}, foo=1).captured


expect_type_error(
    "type-three-args", case_type_three_args,
    "X.__init_subclass__() takes no keyword arguments")
assert case_type_three_args_consumed() == {"foo": 1}


def case_explicit_super():
    class C0:
        pass

    return super(C0, C0).__init_subclass__(zz=1)


def case_explicit_super_returns_none():
    class C1:
        pass

    return super(C1, C1).__init_subclass__()


expect_type_error(
    "explicit-super", case_explicit_super,
    "case_explicit_super.<locals>.C0.__init_subclass__() takes no keyword arguments")
assert case_explicit_super_returns_none() is None

expect_type_error(
    "direct-call", lambda: Plain.__init_subclass__(foo=1),
    "Plain.__init_subclass__() takes no keyword arguments")
assert Plain.__init_subclass__() is None

assert object.__init_subclass__() is None
expect_type_error(
    "object-hook-kw", lambda: object.__init_subclass__(foo=1),
    "object.__init_subclass__() takes no keyword arguments")
# CPython: object.__init_subclass__() takes no arguments (1 given)
expect_type_error("object-hook-pos", lambda: object.__init_subclass__(1))


# ---------------------------------------------------------------------------
# a hook on the metaclass is not consulted, and neither is the new class's own
# ---------------------------------------------------------------------------
class MetaHook(type):
    def __init_subclass__(cls, **kw):
        cls.meta_seen = kw


def case_metaclass_hook():
    class MChild(metaclass=MetaHook, z=1):
        pass


def case_own_hook():
    class SelfHook(Plain, x=1):
        def __init_subclass__(cls, **kw):
            cls.seen = kw


expect_type_error(
    "metaclass-hook", case_metaclass_hook,
    "case_metaclass_hook.<locals>.MChild.__init_subclass__() takes no keyword arguments")
expect_type_error(
    "own-hook", case_own_hook,
    "case_own_hook.<locals>.SelfHook.__init_subclass__() takes no keyword arguments")


# a metaclass that consumes the keywords itself leaves nothing to reject
class MetaNew(type):
    def __new__(mcls, name, bases, ns, **kw):
        cls = type.__new__(mcls, name, bases, ns)
        cls.meta_kw = kw
        return cls


class MetaInit(type):
    def __new__(mcls, name, bases, ns, **kw):
        return type.__new__(mcls, name, bases, ns)

    def __init__(cls, name, bases, ns, **kw):
        cls.meta_kw = kw
        super().__init__(name, bases, ns)


def case_metaclass_consumes():
    class GMetaNew(metaclass=MetaNew, hello=1):
        pass

    class GMetaInit(metaclass=MetaInit, hello=2):
        pass

    return (GMetaNew.meta_kw, GMetaInit.meta_kw)


assert case_metaclass_consumes() == ({"hello": 1}, {"hello": 2})


# ---------------------------------------------------------------------------
# PEP 695 generic classes take the same path
# ---------------------------------------------------------------------------
def case_pep695_kwargs():
    class Foo[T](Plain, x=1):
        pass


def case_pep695_consumed():
    class Bar[T](Consumer, y=1):
        pass

    return Bar.captured


expect_type_error(
    "pep695-kwargs", case_pep695_kwargs,
    "case_pep695_kwargs.<locals>.Foo.__init_subclass__() takes no keyword arguments")
assert case_pep695_consumed() == {"y": 1}


# ---------------------------------------------------------------------------
# hooks that forward only what they were given, and nested class statements
# ---------------------------------------------------------------------------
class TightForwarder:
    def __init_subclass__(cls, **kwargs):
        cls.seen = dict(kwargs)
        super().__init_subclass__()


def case_tight_forwarder():
    class T(TightForwarder, aa=1):
        pass

    return T.seen


def case_nested():
    class Inner:
        pass

    class Outer(Inner, k=2):
        pass


assert case_tight_forwarder() == {"aa": 1}
expect_type_error(
    "nested-class", case_nested,
    "case_nested.<locals>.Outer.__init_subclass__() takes no keyword arguments")


# ---------------------------------------------------------------------------
# paths that reach the default hook through other machinery
# ---------------------------------------------------------------------------
def case_deprecated():
    import warnings

    @warnings.deprecated("old")
    class OldBase:
        pass

    class Dep(OldBase, k=1):
        pass


def case_generic():
    from typing import Generic

    class G2(Generic, x=1):
        pass


def case_qualname_keyword():
    type("C", (Plain,), {}, __qualname__="Q")


# CPython names the new class in both; PySharp names the class the builtin
# bound to (the deprecated base) and the applied qualname
expect_type_error("deprecated-base", case_deprecated)
expect_type_error("generic-base", case_generic)
expect_type_error("qualname-keyword", case_qualname_keyword)


# a consuming hook still sees a __qualname__ keyword as an ordinary keyword
def case_qualname_consumed():
    return type("C", (Consumer,), {}, __qualname__="Q", tag=1).captured


assert case_qualname_consumed() == {"__qualname__": "Q", "tag": 1}

print("ok")
