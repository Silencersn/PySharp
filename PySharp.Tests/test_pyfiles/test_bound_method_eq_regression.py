"""
Regression: bound method objects must compare equal when both __func__
and __self__ are identical (CPython method_richcompare compares by
identity), and their hash must be consistent so methods work in `in`,
set dedup and list.remove. Only ==/!= are supported; custom __eq__ or
__hash__ on the instance does not leak into method comparison or its
hash (PyObject_GenericHash bypasses custom __hash__).
"""

class A:
    def m(self):
        pass

    def n(self):
        pass

a = A()
b = A()

assert (a.m == a.m) is True
assert (a.m == a.n) is False
assert (a.m == b.m) is False
assert (a.m != a.m) is False
assert (a.m != b.m) is True
assert (a.m is a.m) is False
assert (a.m == 5) is False
assert (a.m != 5) is True

# containers: membership, set dedup, list.remove
assert a.m in [a.m, b.m]
assert a.m not in [b.m]
assert len({a.m, a.m, b.m}) == 2
assert hash(a.m) == hash(a.m)
assert hash(a.m) != hash(a.n)
assert hash(a.m) != hash(b.m)


def drop_last(seq):
    seq.remove(seq[-1])
    return seq

assert drop_last([a.m, b.m]) == [a.m]

# __self__/__func__ access and calling still work
assert a.m.__self__ is a
assert a.m.__func__ is A.m
assert (lambda f: 42)(a.m) == 42


class C:
    def m(self, x):
        return x + 1

assert C().m(41) == 42
assert A.m(a) is None

# only ==/!= are supported between methods
try:
    a.m > b.m
except TypeError as e:
    assert str(e) == "'>' not supported between instances of 'method' and 'method'", str(e)
else:
    raise AssertionError("expected TypeError")


# a custom __eq__ on the instance does not make distinct instances'
# methods equal (identity comparison), and a custom __hash__ does not
# leak into the method's hash (consistency still holds)
class Eq:
    def __eq__(self, other):
        return True

    def m(self):
        pass

e1 = Eq()
e2 = Eq()
assert (e1.m == e2.m) is False


class H:
    def __hash__(self):
        return 12345

    def m(self):
        pass

h = H()
assert (h.m == h.m) is True
assert hash(h.m) == hash(h.m)

print("test_bound_method_eq_regression passed")
