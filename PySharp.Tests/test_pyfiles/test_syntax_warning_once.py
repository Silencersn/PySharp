"""
the same SyntaxWarning (same file, line and message) must be
printed to stderr exactly once, matching CPython's default warning filter
dedup behavior.

CPython 3.14 reference:
    print("\\400")   -> exactly one
    SyntaxWarning: "\\400" is an invalid octal escape sequence ...

PySharp used to print the identical warning block 4 times for this
module-level statement (and more times in larger scripts): speculative
parses (statement, generator-expression and group tries) re-converted
the literal, and the parse-time warning path had no dedup. Independent
literals on one line must keep their separate warnings, and a literal
warns about its first invalid escape, not the last.

This file triggers the warnings; the C# side
(TestSyntaxWarningOnceRegression) captures stderr and asserts the
occurrence counts. The doubled backslashes in this docstring are
intentional so the docstring itself raises no warning.

:kind: test
"""

# the red case: a call argument is parsed speculatively and for real
print("\400")

# a single-element tuple passes through the group try and the
# comprehension test before the real parse: one warning, not three
x = ("\400",)

# two independent literals on one line keep two warnings, like CPython
y = "\400" "\400"

# only the first invalid escape of a literal is warned
print("\400 \777")
