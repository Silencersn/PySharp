"""
Regression: for identical operand types CPython's binary_op1 resolves both
operands to a single shared slot, so only the forward variant is tried and
the reflected method (__radd__ etc.) never runs — a class defining only
__radd__ raises TypeError on V + V instead of silently "succeeding".
Comparisons are exempt (a same-type reflected comparison is still tried),
and the subclass-priority rule for different types is unchanged.

CPython 3.14 reference:
    V(1) + V(2), V defines only __radd__      -> TypeError
    T() + T(), __add__ -> NotImplemented,
        __radd__ defined                      -> TypeError (no reflected call)
    T2() + T2(), __add__ wins                 -> 'add'
    A() + B(), B subclass with only __radd__  -> B.__radd__ called
    B() + A() (left is the subclass)          -> TypeError
    CB() + CB(), CB(CA) with only __radd__    -> TypeError
    CB() + CA()                               -> CA.__radd__ called
    L() + R(), unrelated, only __radd__ on R  -> R.__radd__ called
    q < q, q defines only __gt__              -> __gt__ called
    TypeError text for arithmetic             -> unsupported operand type(s) for +: ...
"""

NI = NotImplemented


class V:
    def __init__(self, v):
        self.v = v

    def __radd__(self, other):
        return ('radd', other)


try:
    V(1) + V(2)
except TypeError as e:
    assert str(e) == "unsupported operand type(s) for +: 'V' and 'V'"
else:
    raise AssertionError('same-type + with only __radd__ must raise TypeError')


class T:
    def __add__(self, other):
        return NI

    def __radd__(self, other):
        return ('radd', other)


try:
    T() + T()
except TypeError:
    pass
else:
    raise AssertionError('__add__ returning NotImplemented must not fall back to __radd__ on the same type')


class T2:
    def __add__(self, other):
        return 'add'

    def __radd__(self, other):
        return ('radd', other)


assert T2() + T2() == 'add'


class A:
    pass


class B(A):
    def __radd__(self, other):
        return ('B-radd', other)


# subclass priority: right operand's reflected runs first
result = A() + B()
assert result[0] == 'B-radd'

# left subclass with only __radd__ gets no help
try:
    B() + A()
except TypeError as e:
    assert str(e) == "unsupported operand type(s) for +: 'B' and 'A'"
else:
    raise AssertionError('left subclass with only __radd__ must raise TypeError')


class CA:
    def __radd__(self, other):
        return ('CA-radd', other)


class CB(CA):
    pass


# same (inherited) type: the inherited __radd__ must NOT be tried
try:
    CB() + CB()
except TypeError as e:
    assert str(e) == "unsupported operand type(s) for +: 'CB' and 'CB'"
else:
    raise AssertionError('same-type + must not call the inherited __radd__')

# different types, base on the right: reflected runs
result = CB() + CA()
assert result[0] == 'CA-radd'


class L:
    def __add__(self, other):
        return NI


class R:
    def __radd__(self, other):
        return ('R-radd', other)


result = L() + R()
assert result[0] == 'R-radd'


class Q:
    def __gt__(self, other):
        return 'gt-called'


# comparisons keep the reflected direction for same-type operands
lt_result = Q() < Q()
assert lt_result == 'gt-called'

# builtin same-type arithmetic and comparison messages stay intact
assert True + True == 2
assert 'a' + 'b' == 'ab'
assert [1] + [2] == [1, 2]
try:
    1 < 'a'
except TypeError as e:
    assert str(e) == "'<' not supported between instances of 'int' and 'str'"
else:
    raise AssertionError('1 < str must raise TypeError')

print("test_reflected_arithmetic_same_type_regression passed")
