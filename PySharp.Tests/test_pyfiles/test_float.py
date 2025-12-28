a = 3.5
b = 2.0

assert repr(a) == '3.5'
assert str(a) == '3.5'
assert hash(a) == hash(3.5)
assert bool(a) is True
assert bool(0.0) is False
assert int(a) == 3
assert float(a) == 3.5
assert abs(-a) == 3.5
assert -a == -3.5
assert +a == 3.5
assert a + b == 5.5
assert a - b == 1.5
assert a * b == 7.0
assert a / b == 1.75
assert a // b == 1.0
assert a % b == 1.5
assert divmod(a, b) == (1.0, 1.5)
assert pow(a, b) == 12.25
assert (b + a) == 5.5
assert (b - a) == -1.5
assert (b * a) == 7.0
assert (b / a) == 0.5714285714285714
assert (b // a) == 0.0
assert (b % a) == 2.0
assert divmod(b, a) == (0.0, 2.0)
assert pow(b, a) == 11.313708498984761
assert a < 4.0
assert a <= 3.5
assert a == 3.5
assert a != 2.0
assert a > 2.0
assert a >= 3.5
assert f'{3.14159:.2f}' == '3.14'
assert f'{3.14159:+.2f}' == '+3.14' and f'{-3.14159:+.2f}' == '-3.14'
assert float(5) == 5.0
