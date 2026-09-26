"""
int(True) / int(False) must never retag the bool singletons.
PyIntObjectType.New used to assign cls into obj._pyType unconditionally,
permanently corrupting the process-wide True/False singletons (their type
became int, repr became '1'/'0') - bool has no __int__ override, so the
Int slot handed New the singleton itself.

CPython long_new returns exact ints unchanged and converts subclass
instances to exact ints through the small-int pool, so int(True) is 1
while int(True) is not True. Subclass construction (MyInt(5), MyInt(True))
must keep working on fresh objects - and the small-int pool itself must
stay exact int.

:kind: test
"""

one = 1
zero = 0
five = 5
big = 10 ** 20


# red cases: int(bool) converts, never retags
a = int(True)
assert a is one
assert a is not True
b = int(False)
assert b is zero
assert b is not False


# the singletons must be untouched afterwards
assert type(True).__name__ == "bool"
assert type(False).__name__ == "bool"
assert isinstance(True, bool) is True
assert isinstance(False, bool) is True
assert repr(True) == "True"
assert str(True) == "True"
assert repr(False) == "False"
assert repr([True, False]) == "[True, False]"
assert repr(list({True: "a"}.keys())) == "[True]"


# guards: exact ints keep their identity (long_new returns them as-is)
assert int(1) is one
assert int(big) is big


# guards: subclass construction still works on fresh objects
class MyInt(int):
    pass


x = MyInt(5)
assert type(x).__name__ == "MyInt"
assert repr(x) == "5"
assert isinstance(x, int)
y = MyInt(True)
assert type(y).__name__ == "MyInt"
assert repr(y) == "1"
assert repr(True) == "True"

# the small-int pool must stay exact int after subclass construction
assert type(one).__name__ == "int"
assert type(five).__name__ == "int"
one_again = 1
assert one is one_again

print("test_int_new_bool_singleton passed")
