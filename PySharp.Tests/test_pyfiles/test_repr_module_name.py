"""Verifies default instance and class reprs render the module-qualified name, function reprs use __qualname__ without a module prefix, and GenericAlias rendering follows the same module-qualified rule.

:kind: test
"""

# Regression: default instance and class reprs must render the module
# qualified name (module != "builtins" -> "module.qualname", CPython
# Objects/typeobject.c object_repr/type_repr), function reprs must use
# __qualname__ with no module prefix (Objects/funcobject.c func_repr), and
# GenericAlias rendering shares the same module-qualified rule
# (Objects/typevarobject.c _Py_typing_type_repr). Addresses stay
# address-agnostic on purpose so the corpus runs on CPython too.

class T:
    pass


assert repr(T()).startswith("<__main__.T object at 0x"), repr(T())
assert repr(T) == "<class '__main__.T'>", repr(T)
assert repr(int) == "<class 'int'>", repr(int)
assert repr(object()).startswith("<object object at 0x"), repr(object())


def make():
    class C:
        pass

    return C


C = make()
assert C.__qualname__ == "make.<locals>.C", C.__qualname__
assert repr(C) == "<class '__main__.make.<locals>.C'>", repr(C)
assert repr(C()).startswith("<__main__.make.<locals>.C object at 0x"), repr(C())


def g():
    def inner():
        pass

    return lambda: None


assert repr(g()).startswith("<function g.<locals>.<lambda> at 0x"), repr(g())
assert repr(g).startswith("<function g at 0x"), repr(g)


class S:
    @staticmethod
    def s():
        pass

    def m(self):
        pass


assert repr(S.s).startswith("<function S.s at 0x"), repr(S.s)
assert repr(S().m).startswith("<bound method S.m of <__main__.S object at 0x"), repr(S().m)


class Call:
    def __call__(self, a, **kw):
        pass


try:
    Call()(a=2, **{"a": 1})
except TypeError as e:
    assert str(e).startswith("<__main__.Call object at 0x"), str(e)
else:
    raise AssertionError("expected TypeError from keyword collision")

assert repr(type[int]) == "type[int]", repr(type[int])
assert repr(type[T]) == "type[__main__.T]", repr(type[T])
assert repr(type[C]) == "type[__main__.make.<locals>.C]", repr(type[C])

print("repr module name ok")
