"""str.join reports "can only join an iterable" without naming the argument type.

:kind: test
"""

# Regression: a non-iterable argument fell through to the generic
# iteration-protocol message ('int' object is not iterable). CPython's
# PyUnicode_Join calls PySequence_Fast with the fixed sentence, which
# replaces only the TypeError of the protocol — an exception raised
# inside a custom __iter__/__next__ still passes through unchanged, and
# the per-element check keeps its numbered message.


sep = ","

for label, f in [
    ("int", lambda: sep.join(1)),
    ("none", lambda: sep.join(None)),
    ("float", lambda: sep.join(1.5)),
    ("non-str-item", lambda: sep.join([1])),
    ("ok-pair", lambda: sep.join(["a", "b"])),
    ("ok-genexp", lambda: sep.join(str(i) for i in range(2))),
    ("ok-empty", lambda: sep.join([])),
    ("ok-str-source", lambda: "".join("abc")),
]:
    try:
        print(label, "=", repr(f()))
    except TypeError as e:
        print(label, "! TypeError:", e)


class BadIter:
    def __iter__(self):
        raise ValueError("custom iter failure")


try:
    ",".join(BadIter())
except ValueError as e:
    print("bad-iter ! ValueError:", e)
