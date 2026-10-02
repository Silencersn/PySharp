"""assignment unpacking names a plain non-iterable with its own sentence.

:kind: test
"""

# Regression: every unpack form fell through to the generic iteration
# message. CPython's unpack_iterable swaps in "cannot unpack non-iterable
# <type> object" (tp_name, no quotes) only when the type's MRO carries no
# __iter__ of its own — a TypeError raised by a custom __iter__ passes
# through unchanged, and the for/GET_ITER path keeps the generic message.


def try_unpack(label, fn):
    try:
        fn()
        print(label, "ok")
    except TypeError as e:
        print(label, "! TypeError:", e)
    except ValueError as e:
        print(label, "! ValueError:", e)


for label, fn in [
    ("a,b=int", lambda: exec("a, b = 1", {})),
    ("a,b=float", lambda: exec("a, b = 1.5", {})),
    ("a,b=None", lambda: exec("a, b = None", {})),
    ("a,b=obj", lambda: exec("a, b = object()", {})),
    ("a,b=True", lambda: exec("a, b = True", {})),
    ("a,b,c=int", lambda: exec("a, b, c = 1", {})),
    ("a,=int", lambda: exec("a, = 1", {})),
    ("[a,b]=int", lambda: exec("[a, b] = 1", {})),
    ("a,*b=int", lambda: exec("a, *b = 1", {})),
    ("*a,b=int", lambda: exec("*a, b = 1", {})),
    ("for-in", lambda: exec("for a, b in 1:\n    pass", {})),
    ("str-ok", lambda: exec("a, b = 'xy'", {})),
    ("many", lambda: exec("a, b = [1, 2, 3]", {})),
    ("few", lambda: exec("a, b, c = [1]", {})),
    ("gen-ok", lambda: exec("a, b = (i for i in range(2))", {})),
]:
    try_unpack(label, fn)


class BadIter:
    def __iter__(self):
        raise TypeError("custom boom")


try:
    a, b = BadIter()
except TypeError as e:
    print("bad-iter:", e)


class GetItemOnly:
    def __getitem__(self, i):
        if i > 1:
            raise IndexError
        return i


try:
    a, b = GetItemOnly()
    print("getitem:", a, b)
except Exception as e:
    print("getitem !", type(e).__name__, e)
