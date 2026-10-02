"""bytearray subscript out-of-range errors carry a type-name prefix while bytes stays unprefixed.

:kind: test
"""

# Regression: bytes and bytearray share the plain "index out of range"
# sentence in PySharp, but CPython gives bytearray its own
# "bytearray index out of range" for both reading and writing. The
# prefix holds for positive and negative overflow alike; bytes, list
# and str keep their own established messages.


def show(label, f):
    try:
        r = f()
        print(label, "=", repr(r))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


ba = bytearray(b"abc")
show("ba_get_99", lambda: ba[99])
show("ba_get_m99", lambda: ba[-99])
show("ba_set_99", lambda: ba.__setitem__(99, 1))
show("ba_set_m99", lambda: ba.__setitem__(-99, 1))
show("ba_get_2", lambda: ba[2])
show("ba_set_ok", lambda: (ba.__setitem__(0, 100), ba[0])[1])

b = b"abc"
show("b_get_99", lambda: b[99])
show("b_get_m99", lambda: b[-99])
show("b_get_ok", lambda: b[1])
show("list_get_99", lambda: [1, 2, 3][99])
show("str_get_99", lambda: "abc"[99])
