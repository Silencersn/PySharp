import warnings

# warnings.warn / warn_explicit basics
with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("always")
    warnings.warn("boom")
assert len(records) == 1, records
assert records[0].category is UserWarning, records[0].category
assert str(records[0].message) == "boom", records[0].message

with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("always")
    warnings.warn("boom", UserWarning)
assert len(records) == 1, records
assert records[0].category is UserWarning, records[0].category

# Default DeprecationWarning filtering is enabled for __main__.
with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("default")
    warnings.warn("boom", DeprecationWarning)
assert len(records) == 1, records
assert records[0].category is DeprecationWarning, records[0].category

# resetwarnings clears the default filters.
with warnings.catch_warnings(record=True) as records:
    warnings.simplefilter("always")
    warnings.warn("before", DeprecationWarning)
    warnings.resetwarnings()
    warnings.warn("after", DeprecationWarning)
assert len(records) == 2, records
assert str(records[1].message) == "after", records

# warn_explicit accepts a warning instance and keeps the instance category
with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit(UserWarning("boom"), DeprecationWarning, "f.py", 1)
assert len(records) == 1, records
assert records[0].category is UserWarning, records[0].category
assert str(records[0].message) == "boom", records[0].message

# __index__ conversion for lineno works.
class Indexable:
    def __index__(self):
        return 7

with warnings.catch_warnings(record=True) as records:
    warnings.warn_explicit("m", UserWarning, "f.py", Indexable())
assert len(records) == 1, records
assert records[0].lineno == 7, records[0].lineno

# stacklevel can be an int-like object.
class OneIndexable:
    def __index__(self):
        return 1

with warnings.catch_warnings(record=True) as records:
    warnings.warn("boom", stacklevel=OneIndexable())
assert len(records) == 1, records

# invalid category should raise TypeError
try:
    warnings.warn("oops", int)
    raise AssertionError("int should not be accepted as a warning category")
except TypeError:
    pass

# deprecated() decorator on functions
from warnings import deprecated

@deprecated("use new")
def f():
    return 1

with warnings.catch_warnings(record=True) as records:
    f()
assert len(records) == 1, records
assert records[0].category is DeprecationWarning, records[0].category
assert getattr(f, "__deprecated__") == "use new", getattr(f, "__deprecated__", None)

# deprecated on classes
@deprecated("use B")
class A:
    pass

with warnings.catch_warnings(record=True) as records:
    A()
assert len(records) == 1, records
assert getattr(A, "__deprecated__") == "use B", getattr(A, "__deprecated__", None)

# subclassing a deprecated class warns on subclass creation
@deprecated("use B")
class C:
    pass

with warnings.catch_warnings(record=True) as records:
    class D(C):
        pass
assert len(records) == 1, records

# custom category and category=None are supported
@deprecated("use B", category=None)
def g():
    return 2

with warnings.catch_warnings(record=True) as records:
    g()
assert len(records) == 0, records
assert getattr(g, "__deprecated__") == "use B", getattr(g, "__deprecated__", None)

@deprecated("use B", category=UserWarning)
def h():
    return 3

with warnings.catch_warnings(record=True) as records:
    h()
assert len(records) == 1, records
assert records[0].category is UserWarning, records[0].category

# invalid message type should raise TypeError
try:
    deprecated(123)
    raise AssertionError("deprecated() should reject non-string messages")
except TypeError:
    pass

print("test_warnings_module_basic passed")
