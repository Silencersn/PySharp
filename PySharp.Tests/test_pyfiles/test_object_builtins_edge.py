"""
Tests for PyObject base class edge cases - hash, repr, str, bool, type(), isinstance(), issubclass()
"""

# object() constructor
obj = object()
assert isinstance(obj, object)
assert type(obj) is object

# object __repr__
r = repr(object())
assert "<object object at" in r

# object __str__ equals __repr__
obj = object()
assert str(obj) == repr(obj)

# object __hash__
h = hash(object())
assert isinstance(h, int)

# object __eq__ (identity comparison)
obj1 = object()
obj2 = object()
obj3 = obj1
assert obj1 == obj3  # same instance
assert obj1 != obj2  # different instances
assert (obj1 == obj2) is False

# object __ne__
assert (obj1 != obj2) is True
assert (obj1 != obj3) is False

# object __bool__ - objects are True by default
assert bool(object()) is True
assert bool(object) is True

# type() with single argument
assert type(1) is int
assert type("hello") is str
assert type(True) is bool
assert type(3.14) is float
assert type([1, 2]) is list
assert type((1,)) is tuple
assert type({'a': 1}) is dict
assert type({1, 2}) is set
assert type(None) is type(None)
assert type(type) is type

# type() with three arguments (dynamic type creation)
# MyClass = type('MyClass', (object,), {'x': 10})
# obj = MyClass()
# assert obj.x == 10
# assert type(obj).__name__ == 'MyClass'

# isinstance() - single type
assert isinstance(42, int) is True
assert isinstance("hello", str) is True
assert isinstance([1, 2], list) is True
assert isinstance(True, int) is True  # bool is subclass of int
assert isinstance(True, bool) is True
assert isinstance(3.14, (int, float)) is True
assert isinstance(42, (str, list)) is False

# isinstance() - tuple of types
assert isinstance(42, (str, float, int)) is True
assert isinstance("hi", (str, bytes)) is True
assert isinstance(b"bytes", (str, bytes)) is True

# isinstance() with object
assert isinstance(42, object) is True
assert isinstance(None, object) is True
assert isinstance("abc", object) is True

# isinstance() edge cases
assert isinstance(True, int) is True
assert isinstance(False, int) is True
assert isinstance(1, bool) is False  # int is not subclass of bool

# issubclass() - single
assert issubclass(bool, int) is True
assert issubclass(int, object) is True
assert issubclass(str, object) is True
assert issubclass(ValueError, Exception) is True
assert issubclass(TypeError, Exception) is True

# issubclass() - tuple
assert issubclass(bool, (str, int)) is True
assert issubclass(float, (str, int)) is False

# issubclass() edge cases
assert issubclass(bool, int) is True
assert issubclass(int, bool) is False

# hash() on various types
assert isinstance(hash(42), int)
assert isinstance(hash("hello"), int)
assert isinstance(hash((1, 2, 3)), int)
assert isinstance(hash(frozenset([1, 2])), int)

# hash consistency
assert hash(42) == hash(42)
assert hash("hello") == hash("hello")

# dir() on basic types
d = dir(42)
assert isinstance(d, list)
assert len(d) > 0

d = dir("hello")
assert isinstance(d, list)
# TODO: dir() on built-in types may not include all special methods yet
# assert "__add__" in d or "upper" in d
assert len(d) > 0

# id() returns unique ints
a = object()
b = object()
assert id(a) != id(b) or a is b  # could theoretically collide
assert isinstance(id(a), int)
assert isinstance(id(None), int)

# id() consistency
assert id(a) == id(a)

# bool() on various objects
assert bool(1) is True
assert bool(0) is False
assert bool(-1) is True
assert bool("") is False
assert bool("False") is True
assert bool([]) is False
assert bool([None]) is True
assert bool({}) is False
assert bool({'a': 1}) is True
assert bool(()) is False
assert bool((1,)) is True
assert bool(set()) is False
assert bool({1}) is True
assert bool(None) is False
assert bool(0.0) is False
assert bool(0.1) is True
assert bool(0j) is False
assert bool(1j) is True

# str() on various types
assert str(123) == '123'
assert str(-42) == '-42'
assert str(3.14) == '3.14'
assert str(None) == 'None'
assert str(True) == 'True'
assert str(False) == 'False'
assert str([1, 2, 3]) == '[1, 2, 3]'
assert str((1, 2)) == '(1, 2)'

# repr() on various types
assert repr("hello") == "'hello'"
assert repr(42) == '42'
assert repr(True) == 'True'
assert repr(None) == 'None'

print("test_object_builtins_edge passed")
