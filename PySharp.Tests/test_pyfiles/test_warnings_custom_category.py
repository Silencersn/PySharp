"""
warnings.warn with a user-defined Warning subclass must
record the warning like any built-in category. CreateWarningInstance
hard-cast the category to the built-in PyExceptionType, so a custom
subclass (a UserDefinedType<PyExceptionObject>) crashed the process
with a .NET InvalidCastException instead of emitting the warning.

CPython 3.14 reference: the category only needs to be a Warning
subclass; the instance is created by calling the category with the
message (Python/_warnings.c).

:kind: test
"""

import warnings

class MyWarning(Warning):
    pass

# user-defined category is recorded
with warnings.catch_warnings(record=True) as caught:
    warnings.simplefilter("always")
    warnings.warn("custom", MyWarning)
assert len(caught) == 1
assert caught[0].category is MyWarning
assert str(caught[0].message) == "custom"

# passing a Warning instance keeps its own category
with warnings.catch_warnings(record=True) as caught2:
    warnings.simplefilter("always")
    warnings.warn(MyWarning("already"))
assert len(caught2) == 1
assert caught2[0].category is MyWarning
assert str(caught2[0].message) == "already"

# built-in categories unchanged
with warnings.catch_warnings(record=True) as caught3:
    warnings.simplefilter("always")
    warnings.warn("builtin", UserWarning)
    warnings.warn("dep", DeprecationWarning)
assert [c.category.__name__ for c in caught3] == ["UserWarning", "DeprecationWarning"]

# a non-Warning category is a TypeError, not a crash
try:
    warnings.warn("x", int)
    assert False, "TypeError expected"
except TypeError as e:
    assert "category must be a Warning subclass" in str(e)

# a subclass with a custom __init__ receives the message
class Fancy(Warning):
    def __init__(self, msg, extra="none"):
        super().__init__(msg)
        self.extra = extra

with warnings.catch_warnings(record=True) as caught4:
    warnings.simplefilter("always")
    warnings.warn("fancy", Fancy)
assert len(caught4) == 1
assert str(caught4[0].message) == "fancy"
assert type(caught4[0].message).__name__ == "Fancy"

# the error filter raises the custom category itself
with warnings.catch_warnings():
    warnings.simplefilter("error")
    try:
        warnings.warn("boom", MyWarning)
        assert False, "MyWarning expected"
    except MyWarning as e:
        assert str(e) == "boom"

print("test_warnings_custom_category passed")
