"""
Regression test: __class__ cell propagation through nested function chain.

Tests that when a metaclass __new__ defines nested closures whose inner
functions use super(), the __class__ cell variable is properly propagated
through ALL intermediate function scopes, not just the innermost one.
"""

# Test 1: basic chain - metaclass __new__ -> outer -> inner (with super)
class Meta1(type):
    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)

        def outer(cls):
            def inner(self):
                super(cls, self).__init__()
            return inner

        cls.method = outer(cls)
        return cls

class Foo1(metaclass=Meta1):
    def __init__(self):
        pass

obj1 = Foo1()
assert hasattr(obj1, 'method')
obj1.method()

# Test 2: three-level nesting
class Meta2(type):
    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)

        def level1(cls):
            def level2():
                def inner(self):
                    super(cls, self).__init__()
                return inner
            return level2()

        cls.method = level1(cls)
        return cls

class Foo2(metaclass=Meta2):
    pass

obj2 = Foo2()
obj2.method()

# Test 3: multiple methods in same class, all needing __class__
captured = []

class Meta3(type):
    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)

        def maker1(cls):
            def f1(self):
                super(cls, self).__init__()
            return f1

        def maker2(cls):
            def f2(self):
                super(cls, self).__init__()
            return f2

        cls.f1 = maker1(cls)
        cls.f2 = maker2(cls)
        return cls

class Foo3(metaclass=Meta3):
    pass

obj3 = Foo3()
obj3.f1()
obj3.f2()

# Test 4: class without any super usage (should still work)
class Meta4(type):
    def __new__(mcs, name, bases, namespace, **kwargs):
        cls = super().__new__(mcs, name, bases, namespace, **kwargs)

        def outer(cls):
            def inner(self):
                return 42
            return inner

        cls.method = outer(cls)
        return cls

class Foo4(metaclass=Meta4):
    pass

obj4 = Foo4()
assert obj4.method() == 42

print("test_class_closure passed")
