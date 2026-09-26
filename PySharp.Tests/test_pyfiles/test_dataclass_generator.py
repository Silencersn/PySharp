"""
dataclass generator deep fixes. The generated __init__ merges
fields from dataclass bases (base order, child overrides), field() gains
keyword-only parameters with metadata (wrapped mappingproxy-style) and
kw_only, class-level kw_only=True makes every field keyword-only, the
generated __init__ calls __post_init__ with the InitVar values at the
end, InitVar pseudo-fields take __init__ parameters without becoming
instance attributes, KW_ONLY switches following fields to keyword-only,
and __match_args__ is generated for class-pattern matching.

CPython 3.14 reference (Lib/dataclasses.py _process_class/_get_field/
_init_fn/_fields_in_init_order, field()).

:kind: test
"""

import dataclasses
from dataclasses import dataclass, field, InitVar, KW_ONLY

# --- Face A: inherited fields merge into the generated __init__ ---
@dataclass
class Base:
    a: int = 1

@dataclass
class Child(Base):
    b: str = "b"

assert repr(Child(9, "z")) == "Child(a=9, b='z')", repr(Child(9, "z"))
assert repr(Child()) == "Child(a=1, b='b')"
assert repr(Child(b="q")) == "Child(a=1, b='q')"
assert list(Child.__dataclass_fields__.keys()) == ["a", "b"]

# three levels and child override of a base field default
@dataclass
class Mid(Base):
    x: int = 10

@dataclass
class Leaf(Mid):
    y: int = 20

assert repr(Leaf()) == "Leaf(a=1, x=10, y=20)"
assert repr(Leaf(1, 2, 3)) == "Leaf(a=1, x=2, y=3)"

# non-default after default across the inheritance boundary
try:
    @dataclass
    class BadChild(Base):
        c: int
    assert False, "inheritance order violation must raise"
except TypeError as e:
    assert str(e) == "non-default argument 'c' follows default argument 'a'", str(e)

# --- Face B: field(metadata=...) ---
@dataclass
class M:
    x: int = field(default=0, metadata={"unit": "m"})
    y: str = "s"

assert M.__dataclass_fields__["x"].metadata["unit"] == "m"
assert dict(M.__dataclass_fields__["x"].metadata) == {"unit": "m"}
assert M.__dataclass_fields__["y"].metadata == {}
assert repr(M.__dataclass_fields__["x"].metadata) == "mappingproxy({'unit': 'm'})"
assert repr(M()) == "M(x=0, y='s')"

# --- Face C: __post_init__ hook ---
log = []

@dataclass
class P:
    x: int
    def __post_init__(self):
        log.append(self.x)
        self.x = self.x * 2

assert P(42).x == 84
assert log == [42]

# --- Face D: class-level kw_only ---
@dataclass(kw_only=True)
class K:
    x: int
    y: int = 2

assert K(x=5).x == 5
assert (K(x=1, y=9).x, K(x=1, y=9).y) == (1, 9)
assert K.__match_args__ == ()
try:
    K(5)
    assert False, "kw_only fields reject positional arguments"
except TypeError:
    pass

# kw_only frees the default-order rule among keyword-only fields
@dataclass(kw_only=True)
class KO:
    a: int = 1
    b: str

assert repr(KO(b="x")) == "KO(a=1, b='x')"

# --- Face E: InitVar ---
@dataclass
class IV:
    v: int
    raw: InitVar[int]
    def __post_init__(self, raw):
        self.v = raw

assert IV(3, raw=10).v == 10
assert IV(3, 7).v == 7

ins = IV(1, 2)
assert not hasattr(ins, "raw") and ins.v == 2, "InitVar must not become an attribute"

@dataclass
class IVD:
    v: int = 0
    raw: InitVar[int] = 5
    def __post_init__(self, raw):
        self.v = raw + 1

assert IVD().v == 6
assert IVD(raw=1).v == 2

@dataclass
class PI:
    a: InitVar[int]
    b: InitVar[int]
    v: int = 0
    def __post_init__(self, a, b):
        self.v = a + b

assert PI(1, 2).v == 3

# --- Face F: __match_args__ ---
@dataclass
class MP:
    x: int
    y: str = "y"

assert MP.__match_args__ == ("x", "y")

def match_it():
    p = MP(1, "s")
    match p:
        case MP(1, "s"):
            return "matched"
    return "nomatch"

assert match_it() == "matched"

# --- Face G: field-level kw_only ---
@dataclass
class KG:
    a: int = 1
    b: int = field(kw_only=True, default=2)

assert KG(b=3).b == 3
assert (KG().a, KG().b) == (1, 2)
assert KG.__match_args__ == ("a",)

# KW_ONLY marker switches following fields
@dataclass
class KW:
    a: int
    _: KW_ONLY
    b: int = 2
    c: int = 3

assert (KW(1).a, KW(1, b=8, c=9).c) == (1, 9)
assert repr(KW(1)) == "KW(a=1, b=2, c=3)"

# mixed default positional and kw_only non-default is legal
@dataclass
class MIX:
    a: int = 1
    b: int = field(kw_only=True)

assert repr(MIX(b=2)) == "MIX(a=1, b=2)"

# non-kw_only guards
@dataclass
class R:
    x: int
    y: str = "s"

assert repr(R(1)) == "R(x=1, y='s')"
assert (R(1) == R(1), R(1) == R(2)) == (True, False)

@dataclass
class IF:
    x: int = 0
    tag: str = field(init=False, default="T")

assert (repr(IF()), IF(5).tag) == ("IF(x=0, tag='T')", "T")

@dataclass
class IFF:
    x: int = 0
    items: list = field(init=False, default_factory=list)

i1 = IFF()
i1.items.append(9)
assert repr(i1) == "IFF(x=0, items=[9])" and repr(IFF()) == "IFF(x=0, items=[])"

try:
    field(default=1, default_factory=list)
    assert False, "default plus default_factory must raise"
except ValueError as e:
    assert str(e) == "cannot specify both default and default_factory", str(e)

print("dataclass generator passed")
