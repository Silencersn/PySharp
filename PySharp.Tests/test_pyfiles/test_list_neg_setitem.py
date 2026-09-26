"""
item assignment on a list must map negative indices to
positive ones, like CPython's list_ass_subscript (Objects/listobject.c)
which does index += size before storing. PySharp passed the raw
negative index to the .NET backing store, so every form of negative
item assignment crashed the process with a .NET
ArgumentOutOfRangeException. Out-of-range assignment and deletion
also report CPython's assignment message, not the read-path one.

:kind: test
"""


def expect_index_error(fn, message):
    try:
        fn()
    except IndexError as e:
        assert str(e) == message, str(e)
    else:
        assert False, "should raise IndexError"


# red cases: every negative-item-assignment form used to crash
lst = [1, 2]
lst[-1] = 9
assert lst == [1, 9]

lst.__setitem__(-2, 7)
assert lst == [7, 9]

lst[-2], lst[-1] = 1, 2
assert lst == [1, 2]


class NegOne:
    def __index__(self):
        return -1


lst[NegOne()] = 3
assert lst == [1, 3]


class MyList(list):
    pass


sub = MyList([1, 2])
sub[-1] = 5
assert list(sub) == [1, 5]

# control: out of range reports the assignment message on both sides
expect_index_error(lambda: lst.__setitem__(5, 0),
                   "list assignment index out of range")
expect_index_error(lambda: lst.__setitem__(-10, 0),
                   "list assignment index out of range")

try:
    del lst[5]
except IndexError as e:
    assert str(e) == "list assignment index out of range", str(e)
else:
    assert False, "should raise IndexError"

# control: a huge index reports the cannot-fit message, not a bare
# .NET OverflowError
expect_index_error(lambda: lst.__setitem__(10**100, 0),
                   "cannot fit 'int' into an index-sized integer")

try:
    del lst[10**100]
except IndexError as e:
    assert str(e) == "cannot fit 'int' into an index-sized integer", str(e)
else:
    assert False, "should raise IndexError"

# guards: reads, deletes and slice assignment keep working
assert lst[-1] == 3
del lst[-1]
assert lst == [1]

lst[-1:] = [4, 5]
assert lst == [4, 5]

print("test_list_neg_setitem passed")
