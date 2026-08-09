"""
Regression: hash() must return the built-in int, never an int subclass
instance. hash(MyInt(9)) had type MyInt in PySharp; CPython returns int.

CPython 3.14 reference:
    type(hash(MyInt(9))) is int   (value 9)
    type(hash(True)) is int       (value 1)
    {MyInt(9): 'a'}[9] == 'a', {9: 'a'}[MyInt(9)] == 'a'
"""

class MyInt(int):
    def __new__(cls, x):
        return super().__new__(cls, x)

i = MyInt(9)
assert i == 9
assert type(i) is MyInt

# hash() must return built-in int, not the subclass instance
hi = hash(i)
assert hi == 9
assert type(hi) is int

# bool is an int subclass too: hash(True) is int, not bool
ht = hash(True)
assert ht == 1
assert type(ht) is int

# plain int hash unchanged
h1 = hash(1)
assert h1 == 1
assert type(h1) is int

# -1 sentinel still applies
hneg = hash(-1)
assert hneg == -2
assert type(hneg) is int

# int subclass instances still work as dict keys mixed with int
assert {MyInt(9): 'a'}[9] == 'a'
assert {9: 'a'}[MyInt(9)] == 'a'

print("test_hash_subclass_regression passed")
