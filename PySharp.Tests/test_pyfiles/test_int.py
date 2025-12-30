a = 7
b = 3

assert repr(a) == '7'
assert str(a) == '7'
assert hash(a) == hash(7)
assert bool(a) is True
assert bool(0) is False
assert int(a) == 7
assert float(a) == 7.0
assert abs(-a) == 7
assert abs(a) == 7
assert -a == -7
assert +a == 7
assert a + b == 10
assert a - b == 4
assert a * b == 21
assert a / b == 7 / 3
assert a // b == 2
assert a % b == 1
assert divmod(a, b) == (2, 1)
assert pow(a, b) == 343
assert (b + a) == 10
assert (b - a) == -4
assert (b * a) == 21
assert (b / a) == 3 / 7
assert (b // a) == 0
assert (b % a) == 3
assert divmod(b, a) == (0, 3)
assert pow(b, a) == 2187
assert a < 8
assert a <= 7
assert a == 7
assert a != 3
assert a > 3
assert a >= 7
assert int(5.0) == 5

assert ~a == -8
assert a << b == 56
assert a >> b == 0
assert a & b == 3
assert a | b == 7
assert a ^ b == 4

assert ~b == -4
assert b << a == 384
assert b >> a == 0
assert b & a == 3
assert b | a == 7
assert b ^ a == 4

assert (a < b) is False
assert (a <= b) is False
assert (a == b) is False
assert (a != b) is True
assert (a > b) is True
assert (a >= b) is True

assert int.__add__(a, b) == 10
assert int.__sub__(a, b) == 4
assert int.__mul__(a, b) == 21
assert int.__truediv__(a, b) == 7 / 3
assert int.__floordiv__(a, b) == 2
assert int.__mod__(a, b) == 1
assert int.__pow__(a, b, None) == 343
assert int.__and__(a, b) == 3
assert int.__or__(a, b) == 7
assert int.__xor__(a, b) == 4
assert int.__lshift__(a, b) == 56
assert int.__rshift__(a, b) == 0
assert int.__neg__(a) == -7
assert int.__pos__(a) == 7
assert int.__abs__(-a) == 7
assert int.__invert__(a) == -8

assert int.__lt__(a, b) is False
assert int.__le__(a, b) is False
assert int.__eq__(a, b) is False
assert int.__ne__(a, b) is True
assert int.__gt__(a, b) is True
assert int.__ge__(a, b) is True


class MyInt(int):
	def __new__(cls, value):
		return super().__new__(cls, value)


a = MyInt(7)
b = MyInt(3)

assert repr(a) == '7'
assert str(a) == '7'
assert hash(a) == hash(7)
assert bool(a) is True
assert bool(0) is False
assert int(a) == 7
assert float(a) == 7.0
assert abs(-a) == 7
assert abs(a) == 7
assert -a == -7
assert +a == 7
assert a + b == 10
assert a - b == 4
assert a * b == 21
assert a / b == 7 / 3
assert a // b == 2
assert a % b == 1
assert divmod(a, b) == (2, 1)
assert pow(a, b) == 343
assert (b + a) == 10
assert (b - a) == -4
assert (b * a) == 21
assert (b / a) == 3 / 7
assert (b // a) == 0
assert (b % a) == 3
assert divmod(b, a) == (0, 3)
assert pow(b, a) == 2187
assert a < 8
assert a <= 7
assert a == 7
assert a != 3
assert a > 3
assert a >= 7
assert int(5.0) == 5

assert ~a == -8
assert a << b == 56
assert a >> b == 0
assert a & b == 3
assert a | b == 7
assert a ^ b == 4

assert ~b == -4
assert b << a == 384
assert b >> a == 0
assert b & a == 3
assert b | a == 7
assert b ^ a == 4

assert (a < b) is False
assert (a <= b) is False
assert (a == b) is False
assert (a != b) is True
assert (a > b) is True
assert (a >= b) is True

assert list(range(-5, 3)) == [-5, -4, -3, -2, -1, 0, 1, 2]

assert int.__add__(a, b) == 10
assert int.__sub__(a, b) == 4
assert int.__mul__(a, b) == 21
assert int.__truediv__(a, b) == 7 / 3
assert int.__floordiv__(a, b) == 2
assert int.__mod__(a, b) == 1
assert int.__pow__(a, b, None) == 343
assert int.__and__(a, b) == 3
assert int.__or__(a, b) == 7
assert int.__xor__(a, b) == 4
assert int.__lshift__(a, b) == 56
assert int.__rshift__(a, b) == 0
assert int.__neg__(a) == -7
assert int.__pos__(a) == 7
assert int.__abs__(-a) == 7
assert int.__invert__(a) == -8

assert int.__lt__(a, b) is False
assert int.__le__(a, b) is False
assert int.__eq__(a, b) is False
assert int.__ne__(a, b) is True
assert int.__gt__(a, b) is True
assert int.__ge__(a, b) is True