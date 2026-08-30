# Basic dataclass behavior tests.
from dataclasses import dataclass, field

@dataclass
class Point:
    x: int
    y: int = 0

p = Point(1, 2)
assert p.x == 1
assert p.y == 2
q = Point(5)
assert q.x == 5
assert q.y == 0
r = Point(x=7, y=8)
assert r.x == 7
assert r.y == 8

assert repr(p) == 'Point(x=1, y=2)', repr(p)

assert Point(1, 2) == Point(1, 2)
assert Point(1, 2) != Point(1, 3)
assert Point(1, 2) != 'x'

assert 'x' in Point.__dataclass_fields__
assert Point.__dataclass_fields__['x'].name == 'x'

@dataclass
class Config:
    name: str
    items: list = field(default_factory=list)
    count: int = field(default=3)

c = Config('cfg')
assert c.name == 'cfg'
assert c.items == []
assert c.count == 3
c.items.append(1)
assert c.items == [1]
c2 = Config('cfg')
assert c2.items == []

@dataclass
class Empty:
    pass

e = Empty()
assert repr(e) == 'Empty()', repr(e)

@dataclass(init=False, repr=False)
class Manual:
    x: int = 10
    def __init__(self):
        self.x = 99

m = Manual()
assert m.x == 99

print('test_dataclass passed')
