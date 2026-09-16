"""
Regression: docstrings bound to __doc__ (function, class, module — the
first statement being a string literal) are cleaned at compile time with
CPython's _PyCompile_CleanDoc semantics: expandtabs first, then the first
line loses its leading spaces and every non-blank line after the first
loses the common leading margin (blank lines and lines with less
indentation are left as-is). Unlike inspect.cleandoc, blank edge lines
are kept. Non-docstring string constants keep their raw form, and the
exec compile path shares the cleanup.

CPython 3.14 reference (Python/compile.c _PyCompile_CleanDoc, codegen.c
codegen_body docstring binding).
"""


def f():
    """line1
    line2
        indented
    """
    return 1

assert f.__doc__ == 'line1\nline2\n    indented\n', repr(f.__doc__)


class C:
    """class doc
    second line
        deeper
    """

assert C.__doc__ == 'class doc\nsecond line\n    deeper\n', repr(C.__doc__)


def g():
    """
    leading blank line
    more
    """

assert g.__doc__ == '\nleading blank line\nmore\n', repr(g.__doc__)


def h():
    """no trailing newline
    second"""

assert h.__doc__ == 'no trailing newline\nsecond', repr(h.__doc__)


def t():
    """top
      wider indent
    normal
      wider again
"""

assert t.__doc__ == 'top\n  wider indent\nnormal\n  wider again\n', repr(t.__doc__)


def z():
    """tab	docstring
	more
"""

# expandtabs(8) runs before the margin strip, so the source tabs widen
assert z.__doc__ == 'tab     docstring\nmore\n', repr(z.__doc__)


def b():
    """


    after blanks
    more


"""

assert b.__doc__ == '\n\n\nafter blanks\nmore\n\n\n', repr(b.__doc__)


def k():
    """single line stays"""

assert k.__doc__ == 'single line stays', repr(k.__doc__)


def n():
    pass

assert n.__doc__ is None

# non-docstring string constants are untouched
s = """
not a docstring
    indented
"""
assert s == '\nnot a docstring\n    indented\n', repr(s)

# exec compiles through the same cleanup
ns = {}
exec("class K:\n    \"\"\"doc1\n    doc2\"\"\"", ns)
assert ns["K"].__doc__ == 'doc1\ndoc2', repr(ns["K"].__doc__)

ns2 = {}
exec("def F():\n    '''a\n     b\n    c'''\n", ns2)
assert ns2["F"].__doc__ == 'a\n b\nc', repr(ns2["F"].__doc__)

print("docstring dedent regression passed")
