"""@dataclass rejects mutable field defaults (list/dict/set/bytearray and their subclasses, including via field(default=...)) at class definition time with CPython's exact ValueError, while default_factory and hashable defaults stay accepted.

:kind: test
"""

# Regression: @dataclass rejects mutable field defaults at class
# definition time like CPython's _get_field — any default whose type
# is (or derives from) list/dict/set/bytearray raises
# "mutable default <class 'T'> for field <name> is not allowed: use
# default_factory", while field(default_factory=...) and hashable
# defaults stay accepted.
import dataclasses
from dataclasses import field

def expect_mutable_error(fn, cls_name, field_name):
    try:
        fn()
        raise AssertionError("expected ValueError")
    except ValueError as e:
        assert str(e) == ("mutable default <class '" + cls_name + "'> for field "
                          + field_name + " is not allowed: use default_factory"), str(e)

def plain_list():
    @dataclasses.dataclass
    class C:
        items: list = []

expect_mutable_error(plain_list, "list", "items")

def plain_dict():
    @dataclasses.dataclass
    class C:
        m: dict = {}

expect_mutable_error(plain_dict, "dict", "m")

def plain_set():
    @dataclasses.dataclass
    class C:
        s: set = set()

expect_mutable_error(plain_set, "set", "s")

def plain_bytearray():
    @dataclasses.dataclass
    class C:
        b: bytearray = bytearray()

expect_mutable_error(plain_bytearray, "bytearray", "b")

def via_field():
    @dataclasses.dataclass
    class C:
        items: list = field(default=[])

expect_mutable_error(via_field, "list", "items")

def list_subclass_default():
    class Sub(list):
        pass
    @dataclasses.dataclass
    class C:
        v: Sub = Sub()

try:
    list_subclass_default()
    raise AssertionError("expected ValueError")
except ValueError as e:
    # the embedded class repr differs between runtimes, so only the
    # fixed parts of the message are asserted here
    assert str(e).startswith("mutable default <class '"), str(e)
    assert str(e).endswith("'> for field v is not allowed: use default_factory"), str(e)

# mutable error takes precedence over the ordering check
def mutable_before_ordering():
    @dataclasses.dataclass
    class C:
        x: list = []
        y: int

expect_mutable_error(mutable_before_ordering, "list", "x")

# default_factory stays accepted and per-instance
@dataclasses.dataclass
class WithFactory:
    items: list = field(default_factory=list)

a = WithFactory()
a.items.append(1)
assert WithFactory().items == []

# hashable defaults stay accepted
@dataclasses.dataclass
class WithDefaults:
    x: int = 5
    name: str = "n"
    t: tuple = (1, 2)
    n: None = None

c = WithDefaults()
assert (c.x, c.name, c.t, c.n) == (5, "n", (1, 2), None)

# the basic dataclass face is untouched
@dataclasses.dataclass
class Point:
    x: int
    y: int = 0

p = Point(3)
assert p.y == 0
assert p == Point(3, 0)
assert repr(p) == "Point(x=3, y=0)"

print("test_dataclass_mutable_default passed")
