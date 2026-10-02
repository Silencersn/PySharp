"""Subscripting a sequence with a non-index operand names the container and the operand type, read/assign/delete alike.

:kind: test
"""

# Regression: the subscript entries branch on the index protocol before
# any conversion, so an operand without __index__ reports "<container>
# indices must be integers or slices, not <type>"; str has its own
# wording that quotes the type, and bytes names the singular "byte".
# Operands speaking __index__ keep converting and a broken __index__
# result still reports the conversion sentence.


def show(label, f):
    try:
        print(label, "=", repr(f()))
    except BaseException as e:
        print(label, "!", type(e).__name__, e)


class NoIndex:
    pass


class WithIndex:
    def __index__(self):
        return 1


class BadIndex:
    def __index__(self):
        return "x"


show("list_str", lambda: [1, 2, 3]["a"])
show("list_float", lambda: [1, 2, 3][1.5])
show("list_none", lambda: [1, 2, 3][None])
show("list_ellipsis", lambda: [1, 2, 3][...])
show("list_tuple", lambda: [1, 2, 3][1, 2])
show("tuple_str", lambda: (1, 2, 3)["a"])
show("str_str", lambda: "abc"["a"])
show("str_float", lambda: "abc"[1.5])
show("bytes_str", lambda: b"abc"["a"])
show("bytearray_str", lambda: bytearray(b"abc")["a"])
show("range_str", lambda: range(5)["a"])
show("del_list_str", lambda: (lambda l: l.__delitem__("a"))([1, 2, 3]))
show("assign_list_str", lambda: (lambda l: l.__setitem__("a", 1))([1, 2, 3]))
show("assign_ba_str", lambda: (lambda b: b.__setitem__("a", 1))(bytearray(b"abc")))
show("noindex", lambda: [1, 2, 3][NoIndex()])
show("withindex", lambda: [1, 2, 3][WithIndex()])
show("badindex", lambda: [1, 2, 3][BadIndex()])
show("bool_index", lambda: [1, 2, 3][True])
