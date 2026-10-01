"""Slice assignment onto a list rejects a non-iterable value with a fixed sentence that never names the value's type.

:kind: test
"""

# Regression: list slice assignment funnels the value through
# iteration and, when that raises TypeError, the caller's fixed
# sentence replaces it — for the plain step-1 path and the extended
# path alike, so the value's type never enters the message. Values
# that do iterate still assign, and the extended slice's length check
# keeps its own error.


def assign(label, lst, sl, val):
    try:
        lst[sl] = val
        print(label, "ok", lst)
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


def show(label, f):
    try:
        r = f()
        print(label, "=", repr(r))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


assign("ext_int", [1, 2, 3], slice(0, 2, 1), 5)
assign("ext_none", [1, 2, 3], slice(0, 2, 1), None)
assign("ext_float", [1, 2, 3], slice(0, 2, 1), 1.5)
assign("ext_bool", [1, 2, 3], slice(0, 2, 1), True)
assign("plain_int", [1, 2, 3], slice(0, 2), 5)
assign("plain_none", [1, 2, 3], slice(0, 2), None)
assign("ext_ok", [1, 2, 3], slice(0, 2, 1), [9, 8])
assign("ext_list_rvalue", [1, 2, 3], slice(0, 2, 1), range(2))
assign("ext_str_rvalue", [1, 2, 3], slice(0, 2, 1), "xy")
assign("ext_tuple_rvalue", [1, 2, 3], slice(0, 2, 1), (7, 8))
assign("ext_len_mismatch", [1, 2, 3, 4], slice(0, 4, 2), [1, 2, 3])
assign("plain_len_free", [1, 2, 3], slice(0, 2), [7])
assign("plain_grow", [1, 2, 3], slice(1, 1), ["x", "y"])

a = [1, 2, 3, 4]
del a[0:4:2]
print("del_step", a)
del a[0:2]
print("del_plain", a)
show("byte_error_untouched", lambda: [1, 2, 3][5])
