"""NameError/AttributeError gain the display-time spelling hint.

:kind: test
"""

# Regression: the suggestion engine only served the unexpected-keyword
# path. CPython offers suggestions for NameError (frame locals, globals
# and builtins) and AttributeError (the type's MRO dicts, modules
# excluded) as a display-time append (_Py_Offer_Suggestions), so the
# exception's own message — what str() returns — stays clean and a
# caught-and-printed exception shows no hint on either side.


class A:
    def method(self): pass


def probe(label, fn):
    try:
        fn()
        print(label, "no-error")
    except Exception as e:
        print(label, "|", type(e).__name__ + ":", repr(str(e)))


value = 1
apples = 2


def func():
    pass


probe("valu", lambda: valu)
probe("prin", lambda: prin(1))
probe("funcc", lambda: funcc())
probe("apple", lambda: apple)
probe("far", lambda: zzzzqqqq)
probe("keyword-near", lambda: exec("passs\n"))
probe("inst", lambda: A().methd)
probe("type-attr", lambda: A.methd)
probe("str-upper", lambda: "x".upperr)
probe("list-append", lambda: [].apped(1))
probe("module-bare", lambda: __import__("math").sqtr)
probe("no-suggest", lambda: A().zzzzzz)
probe("str-clean", lambda: str(A().methd and "" or A))
