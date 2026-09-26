"""
pop() on an empty list must raise
IndexError: pop from empty list, like CPython's list_pop_impl
(Objects/listobject.c) which special-cases the empty list before any
index handling - so even an explicit index reports the empty message.
PySharp used to report "pop index out of range" for every form,
misleading users toward an index problem instead of the empty list.

:kind: test
"""

def expect_index_error(fn, message_part):
    try:
        fn()
    except IndexError as e:
        assert message_part in str(e), str(e)
    else:
        assert False, "should raise IndexError"


# red cases: empty list wins over any index value
expect_index_error(lambda: [].pop(), "pop from empty list")
expect_index_error(lambda: [].pop(0), "pop from empty list")
expect_index_error(lambda: [].pop(5), "pop from empty list")
expect_index_error(lambda: [].pop(-1), "pop from empty list")

# control: out-of-range index on a non-empty list keeps its own message
expect_index_error(lambda: [1].pop(5), "pop index out of range")
expect_index_error(lambda: [1].pop(-5), "pop index out of range")

# guard: the index type error still precedes the empty check
try:
    [].pop("x")
except TypeError:
    pass
except IndexError:
    assert False, "type error must precede the empty-list check"
else:
    assert False, "should raise TypeError"


# guards: normal pops keep working
assert [1, 2, 3].pop() == 3
assert [1, 2, 3].pop(0) == 1
assert [1, 2, 3].pop(-1) == 3

lst = []
try:
    lst.pop()
except IndexError:
    pass
assert lst == []            # failed pop leaves the list untouched

print("test_list_pop_empty passed")
