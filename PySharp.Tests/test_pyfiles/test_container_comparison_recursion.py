"""
comparison recursion is bounded by the native stack, not by
Python frames. Cyclic container pairs and __eq__ re-entry never enter a
frame, so the frame counters could not stop them from exhausting the
.NET stack and killing the process (uncatchable StackOverflowException);
CPython raises a catchable RecursionError instead. Comparison now probes
the native stack at the richcompare entry and raises RecursionError.

CPython 3.14 reference (Objects/object.c PyObject_RichCompare calls
_Py_EnterRecursiveCall; the soft limit is compared against machine stack
usage, not a call count).

:kind: test
"""


def expect_recursion(fn):
    try:
        fn()
    except RecursionError as exc:
        assert str(exc)
        return
    raise AssertionError("RecursionError not raised")


# ---------------------------------------------------------------- cycles
def cyc_list():
    x = [1]
    x.append(x)
    return x


def cyc_dict():
    y = {"k": 1}
    y["self"] = y
    return y


def cyc_tuple_list():
    t = ([],)
    t[0].append(t)
    return t


def cyc_list_dict():
    a = [1]
    d = {"k": a}
    a.append(d)
    return a


expect_recursion(lambda: cyc_list() == cyc_list())
expect_recursion(lambda: cyc_list() != cyc_list())
expect_recursion(lambda: cyc_list() < cyc_list())
expect_recursion(lambda: cyc_dict() == cyc_dict())
expect_recursion(lambda: cyc_tuple_list() == cyc_tuple_list())
expect_recursion(lambda: cyc_list_dict() == cyc_list_dict())

# the identity shortcut still wins over the guard: a lone cyclic container
# compared with itself never recurses
a = cyc_list()
assert (a == a) is True
assert (a != a) is False
assert (a < a) is False
assert ([a] == [a]) is True
assert (a in [a]) is True
d = cyc_dict()
assert (d == d) is True
assert (d != d) is False


# ------------------------------------------------- __eq__ re-entry (set)
class Reenter:
    def __hash__(self):
        return 5

    def __eq__(self, other):
        target.add(Reenter())
        return False


target = {Reenter()}
expect_recursion(lambda: target.add(Reenter()))


# ------------------------------------------------------- acyclic depth
def nest_list(n):
    x = [0]
    for _ in range(n):
        x = [x]
    return x


def nest_tuple(n):
    x = (0,)
    for _ in range(n):
        x = (x,)
    return x


# far beyond any plausible thread stack: CPython raises RecursionError
# around 40000 levels, PySharp at its own stack budget
expect_recursion(lambda: nest_list(50000) == nest_list(50000))
expect_recursion(lambda: nest_tuple(50000) == nest_tuple(50000))


# ----------------------------------------------- shallow results unchanged
assert [1, 2] == [1, 2]
assert [1, 2] != [1, 3]
assert (1, [2]) == (1, [2])
assert {"a": [1, 2]} == {"a": [1, 2]}
assert [[1], [2]] < [[1], [3]]
assert [1, 2] < [1, 3]
assert 2 in [1, 2, 3]
assert [1, 2].index(2) == 1
assert {1, 2} == {2, 1}
assert 3 not in {1, 2}

# the interpreter survives every caught recursion above
assert {"a": 1}["a"] == 1
assert sorted([[2], [1]]) == [[1], [2]]
print("ok")
