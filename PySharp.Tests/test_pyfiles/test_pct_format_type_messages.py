"""%c and the float family keep their formatting-context sentences.

:kind: test
"""

# Regression: %c fell through to the generic __index__ sentence and the
# float family (%f/%e/%g/%E/%G/%F) leaked the float() constructor's
# message. CPython's formatchar names the offending string length or the
# plain type, and PyFloat_AsDouble says "must be real number, not <type>"
# while a custom __float__ still propagates unchanged. (An __index__-only
# object formatting through %f is a separate tracked gap.)

def probe(label, fn):
    try:
        print(label, "=", repr(fn()))
    except Exception as e:
        print(label, "!", type(e).__name__ + ":", e)

for label, fn in [
    ("c-str2", lambda: "%c" % "ab"),
    ("c-str3", lambda: "%c" % "abc"),
    ("c-float", lambda: "%c" % 2.5),
    ("c-none", lambda: "%c" % None),
    ("c-list", lambda: "%c" % []),
    ("c-high", lambda: "%c" % 1114112),
    ("c-neg", lambda: "%c" % -1),
    ("c-ok-int", lambda: "%c" % 65),
    ("c-ok-tuple", lambda: "%c" % (1,)),
    ("f-str", lambda: "%f" % "s"),
    ("e-str", lambda: "%e" % "s"),
    ("g-str", lambda: "%g" % "s"),
    ("E-str", lambda: "%E" % "s"),
    ("G-str", lambda: "%G" % "s"),
    ("F-str", lambda: "%F" % "s"),
    ("f-ok", lambda: "%f" % 1.5),
    ("f-int", lambda: "%f" % 2),
    ("d-str", lambda: "%d" % "x"),
    ("x-float", lambda: "%x" % 2.5),
    ("s-none", lambda: "%s" % None),
]:
    probe(label, fn)
