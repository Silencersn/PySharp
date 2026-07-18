"""
Regression tests for typing.Generic instantiation behavior.
"""

import typing

g = typing.Generic()
assert g is not None
assert type(g) is typing.Generic

try:
    object.__new__(dict)
    assert False, "object.__new__(dict) should be unsafe"
except TypeError:
    pass

print("test_generic_instantiation passed")