"""
Regression: callable() must probe __call__ on the *type* (CPython
builtin_callable / tp_call semantics) — instance-level __getattr__ and
__getattribute__ hooks must never fire, and their return values must not
fake a True result.

CPython 3.14 reference:
    class T: __getattr__ returns 42 -> callable(T()) is False, hook not called
    class T2: __getattribute__ logs -> callable(T2()) is False, no '__call__' logged
    T3.__call__ = lambda self: 99   -> callable(T3()) is True, T3()() works
    class P: __call__ = property    -> callable(P()) is True
    instance-only __call__ attr     -> callable(instance) is False
"""

calls = []
class HooksEverything:
    def __getattr__(self, name):
        calls.append(name)
        return 42

t = HooksEverything()
assert callable(t) is False
assert calls == []

calls2 = []
class LogsAll:
    def __getattribute__(self, name):
        calls2.append(name)
        return object.__getattribute__(self, name)

assert callable(LogsAll()) is False
assert '__call__' not in calls2

# native callables and plain values
assert callable(int) is True
assert callable(object) is True
assert callable(type) is True
assert callable(len) is True
assert callable(callable) is True
assert callable(object()) is False
assert callable(3) is False
assert callable('x') is False
assert callable(None) is False
assert callable(range) is True
assert callable(range(3)) is False

# class-dict __call__ assigned after creation stays callable
T3 = type('T3', (), {})
assert callable(T3) is True
assert callable(T3()) is False
T3.__call__ = lambda self: 99
assert callable(T3()) is True
assert T3()() == 99

# a property as __call__ still marks the class callable
class P:
    __call__ = property(lambda self: 1)
assert callable(P()) is True

# an instance attribute named __call__ must not fool the check
class W:
    pass
w = W()
w.__call__ = 42
assert callable(w) is False

# a real __call__ method keeps working
class WithCall:
    def __call__(self):
        return 1
assert callable(WithCall) is True
assert callable(WithCall()) is True
assert WithCall()() == 1

print("test_callable_type_level_lookup_regression passed")
