class MyInt:
    def __init__(self, value):
        self.value = value
    def __repr__(self):
        return f"MyInt({self.value})"
    def __str__(self):
        return str(self.value)
    def __hash__(self):
        return hash(self.value)
    def __bool__(self):
        return bool(self.value)
    def __int__(self):
        return int(self.value)
    def __float__(self):
        return float(self.value)
    def __complex__(self):
        return complex(self.value)
    def __index__(self):
        return int(self.value)
    def __len__(self):
        return abs(self.value)
    def __iter__(self):
        return iter(range(self.value))
    def __next__(self):
        raise StopIteration
    def __abs__(self):
        return MyInt(abs(self.value))
    def __neg__(self):
        return MyInt(-self.value)
    def __pos__(self):
        return MyInt(+self.value)
    def __invert__(self):
        return MyInt(~self.value)
    def __contains__(self, item):
        return item == self.value
    def __getitem__(self, key):
        return self.value + key
    def __setitem__(self, key, value):
        self.value = value - key
    def __delitem__(self, key):
        self.value = 0
    def __add__(self, other):
        return MyInt(self.value + (other.value if isinstance(other, MyInt) else other))
    def __sub__(self, other):
        return MyInt(self.value - (other.value if isinstance(other, MyInt) else other))
    def __mul__(self, other):
        return MyInt(self.value * (other.value if isinstance(other, MyInt) else other))
    def __truediv__(self, other):
        return MyInt(self.value // (other.value if isinstance(other, MyInt) else other))
    def __floordiv__(self, other):
        return MyInt(self.value // (other.value if isinstance(other, MyInt) else other))
    def __mod__(self, other):
        return MyInt(self.value % (other.value if isinstance(other, MyInt) else other))
    def __divmod__(self, other):
        o = other.value if isinstance(other, MyInt) else other
        return (MyInt(self.value // o), MyInt(self.value % o))
    def __pow__(self, other, modulo=None):
        o = other.value if isinstance(other, MyInt) else other
        if modulo is None:
            return MyInt(pow(self.value, o))
        return MyInt(pow(self.value, o, modulo))
    def __lshift__(self, other):
        return MyInt(self.value << (other.value if isinstance(other, MyInt) else other))
    def __rshift__(self, other):
        return MyInt(self.value >> (other.value if isinstance(other, MyInt) else other))
    def __and__(self, other):
        return MyInt(self.value & (other.value if isinstance(other, MyInt) else other))
    def __xor__(self, other):
        return MyInt(self.value ^ (other.value if isinstance(other, MyInt) else other))
    def __or__(self, other):
        return MyInt(self.value | (other.value if isinstance(other, MyInt) else other))
    def __radd__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) + self.value)
    def __rsub__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) - self.value)
    def __rmul__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) * self.value)
    def __rtruediv__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) // self.value)
    def __rfloordiv__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) // self.value)
    def __rmod__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) % self.value)
    def __rdivmod__(self, other):
        o = other.value if isinstance(other, MyInt) else other
        return (MyInt(o // self.value), MyInt(o % self.value))
    def __rpow__(self, other, modulo=None):
        o = other.value if isinstance(other, MyInt) else other
        if modulo is None:
            return MyInt(pow(o, self.value))
        return MyInt(pow(o, self.value, modulo))
    def __rlshift__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) << self.value)
    def __rrshift__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) >> self.value)
    def __rand__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) & self.value)
    def __rxor__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) ^ self.value)
    def __ror__(self, other):
        return MyInt((other.value if isinstance(other, MyInt) else other) | self.value)
    def __lt__(self, other):
        return self.value < (other.value if isinstance(other, MyInt) else other)
    def __le__(self, other):
        return self.value <= (other.value if isinstance(other, MyInt) else other)
    def __eq__(self, other):
        return self.value == (other.value if isinstance(other, MyInt) else other)
    def __ne__(self, other):
        return self.value != (other.value if isinstance(other, MyInt) else other)
    def __gt__(self, other):
        return self.value > (other.value if isinstance(other, MyInt) else other)
    def __ge__(self, other):
        return self.value >= (other.value if isinstance(other, MyInt) else other)
    def __get__(self, instance, owner):
        return self
    def __set__(self, instance, value):
        self.value = value
    def __delete__(self, instance):
        self.value = 0
    def __set_name__(self, owner, name):
        self.name = name
    def __call__(self, *args, **kwargs):
        return self.value
    def __missing__(self, key):
        return None
    def __getattr__(self, name):
        return getattr(self.value, name)
    def __setattr__(self, name, value):
        if name == 'value' or name == 'name':
            object.__setattr__(self, name, value)
        else:
            setattr(self.value, name, value)
    def __delattr__(self, name):
        if name == 'value' or name == 'name':
            object.__delattr__(self, name)
        else:
            delattr(self.value, name)
    def __init__(self, value):
        object.__setattr__(self, 'value', value)


a = MyInt(5)
b = MyInt(2)

assert repr(a) == "MyInt(5)"
assert str(a) == "5"
assert hash(a) == hash(5)
assert bool(a) is True
assert int(a) == 5
assert float(a) == 5.0
assert complex(a) == complex(5)
assert a.__index__() == 5
assert len(MyInt(-3)) == 3
assert list(iter(MyInt(3))) == [0, 1, 2]
assert abs(MyInt(-7)).value == 7
assert (-a).value == -5
assert (+a).value == 5
assert (~MyInt(2)).value == ~2
assert 5 in MyInt(5)
assert a[3] == 8
a[1] = 10
assert a.value == 9
del a[1]
assert a.value == 0
assert (MyInt(2) + MyInt(3)).value == 5
assert (MyInt(5) - 2).value == 3
assert (MyInt(3) * 2).value == 6
assert (MyInt(7) // 2).value == 3
assert (MyInt(7) % 4).value == 3
q, r = divmod(MyInt(7), 3)
assert q.value == 2 and r.value == 1
assert (MyInt(2) ** 3).value == 8
assert (MyInt(8) << 1).value == 16
assert (MyInt(8) >> 1).value == 4
assert (MyInt(5) & 3).value == 1
assert (MyInt(5) | 2).value == 7
assert (MyInt(5) ^ 3).value == 6
assert (2 + MyInt(3)).value == 5
assert (5 - MyInt(2)).value == 3
assert (2 * MyInt(3)).value == 6
assert (8 // MyInt(2)).value == 4
assert (7 % MyInt(3)).value == 1
q, r = divmod(7, MyInt(3))
assert q.value == 2 and r.value == 1
assert (2 ** MyInt(3)).value == 8
assert (8 << MyInt(1)).value == 16
assert (8 >> MyInt(1)).value == 4
assert (5 & MyInt(3)).value == 1
assert (5 | MyInt(2)).value == 7
assert (5 ^ MyInt(3)).value == 6
assert MyInt(2) < MyInt(3)
assert MyInt(2) <= MyInt(2)
assert MyInt(2) == MyInt(2)
assert MyInt(2) != MyInt(3)
assert MyInt(3) > MyInt(2)
assert MyInt(3) >= MyInt(2)
assert a() == 0
assert a.__get__(None, None) is a
a.__set__(None, 42)
assert a.value == 42
a.__delete__(None)
assert a.value == 0
a.__set_name__(None, 'test')
assert a.name == 'test'
assert a.__missing__(123) is None