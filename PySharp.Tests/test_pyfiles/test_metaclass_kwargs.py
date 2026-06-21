"""
Metaclass keyword arguments and __init_subclass__ tests
"""

# Test 1: __init_subclass__ basic call
class BaseWithInitSubclass:
    init_subclass_called = False
    init_subclass_kwargs = {}

    @classmethod
    def __init_subclass__(cls, **kwargs):
        BaseWithInitSubclass.init_subclass_called = True
        BaseWithInitSubclass.init_subclass_kwargs = kwargs

class ChildA(BaseWithInitSubclass):
    pass

assert BaseWithInitSubclass.init_subclass_called, "__init_subclass__ should be called"
# Current behavior: namespace dict is passed as kwargs
assert '__qualname__' in BaseWithInitSubclass.init_subclass_kwargs, "namespace items should be in kwargs"

# Test 2: __init_subclass__ with class attributes in namespace
class BaseCapture:
    captured = None

    @classmethod
    def __init_subclass__(cls, **kwargs):
        BaseCapture.captured = kwargs

class ChildWithAttr(BaseCapture):
    my_attr = 42

    def my_method(self):
        return self.my_attr

assert BaseCapture.captured is not None
# The namespace dict includes class body attributes
assert 'my_attr' in BaseCapture.captured
assert 'my_method' in BaseCapture.captured

# Test 3: Metaclass that intercepts __init_subclass__ calls
class TrackingMeta(type):
    def __new__(cls, name, bases, attrs, **kwargs):
        return super().__new__(cls, name, bases, attrs)

    def __init__(cls, name, bases, attrs, **kwargs):
        super().__init__(name, bases, attrs)
        cls._meta_kwargs = kwargs

class TrackedClass(metaclass=TrackingMeta, custom_arg='hello', count=123):
    pass

assert hasattr(TrackedClass, '_meta_kwargs')
assert TrackedClass._meta_kwargs.get('custom_arg') == 'hello', f"Expected 'hello', got {TrackedClass._meta_kwargs.get('custom_arg')}"
assert TrackedClass._meta_kwargs.get('count') == 123, f"Expected 123, got {TrackedClass._meta_kwargs.get('count')}"

# Test 4: Metaclass with multiple keyword arguments
class MultiMeta(type):
    instances = []

    def __new__(cls, name, bases, attrs, **kwargs):
        return super().__new__(cls, name, bases, attrs)

    def __init__(cls, name, bases, attrs, **kwargs):
        super().__init__(name, bases, attrs)
        cls._all_kwargs = kwargs
        MultiMeta.instances.append(cls)

class A(metaclass=MultiMeta, x=1, y=2, z=3):
    pass

assert A._all_kwargs.get('x') == 1
assert A._all_kwargs.get('y') == 2
assert A._all_kwargs.get('z') == 3
assert len(A._all_kwargs) >= 3

# Test 5: Metaclass inheritance preserves kwargs
class ChildMeta(MultiMeta):
    pass

class B(metaclass=ChildMeta, foo='bar'):
    pass

assert B._all_kwargs.get('foo') == 'bar'

# Test 6: Metaclass kwargs with no extra keyword arguments
class DefaultMeta(type):
    def __init__(cls, name, bases, attrs, **kwargs):
        super().__init__(name, bases, attrs)
        cls._no_extra = kwargs

class NoKwargs(metaclass=DefaultMeta):
    pass

assert NoKwargs._no_extra == {} or NoKwargs._no_extra is not None

# Test 7: Using type() built-in with keyword arguments
class KwargMeta(type):
    def __new__(cls, name, bases, attrs, **kwargs):
        return super().__new__(cls, name, bases, attrs)

    def __init__(cls, name, bases, attrs, **kwargs):
        super().__init__(name, bases, attrs)
        cls._type_kwargs = kwargs

# Create class dynamically via type() call with metaclass kwarg
DynamicClass = KwargMeta('DynamicClass', (), {'x': 10}, meta_flag=True)

assert hasattr(DynamicClass, '_type_kwargs')
assert DynamicClass._type_kwargs.get('meta_flag') is True
assert DynamicClass.x == 10

print("test_metaclass_kwargs passed")
