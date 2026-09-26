"""
bool's numeric slots follow the CPython boolobject.c
specialization — bitwise and/or/xor keep bool when both operands are
bool (falling through to the int slots otherwise), while the unary
identity operations inherited from int (+, -, ~, abs) upgrade to int.
The old behavior was exactly inverted: True & False returned int and
+True returned bool, breaking code that relies on type(x) is bool.

:kind: test
"""

t, f = True, False

def tn(x):
    return type(x).__name__

# bitwise ops between bools keep bool
assert tn(t & f) == 'bool' and tn(t | f) == 'bool' and tn(t ^ f) == 'bool'
assert tn(t & t) == 'bool' and tn(f | f) == 'bool'
assert (t & f) is f and (t | f) is t and (t ^ t) is f
assert isinstance(t & f, bool)

# mixed bool/int operands produce int on both sides
assert tn(t & 1) == 'int' and tn(1 & t) == 'int'
assert tn(t | 0) == 'int' and tn(5 & t) == 'int'

# unary + upgrades to int; -, ~, abs already did
assert tn(+t) == 'int' and tn(+f) == 'int'
assert (+t) == 1 and (+f) == 0
assert (+t) is not t and (+f) is not f
assert tn(-t) == 'int' and tn(~t) == 'int'
assert tn(abs(t)) == 'int' and tn(abs(f)) == 'int'
assert abs(t) is not t
assert not isinstance(+t, bool)

# augmented bitwise stays bool, augmented arithmetic upgrades
x = t
x &= f
assert tn(x) == 'bool' and x is f
y = t
y += 1
assert tn(y) == 'int' and y == 2

# other numeric faces unchanged
assert pow(t, f) == 1 and tn(pow(t, f)) == 'int'
assert tn(True + True) == 'int'

print("test_bool_numeric_slots passed")
