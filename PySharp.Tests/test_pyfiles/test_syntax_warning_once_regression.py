"""
Regression: the same SyntaxWarning (same file, line and message) must be
printed to stderr exactly once, matching CPython's default warning filter
dedup behavior.

CPython 3.14 reference:
    print("\\400")   -> exactly one
    SyntaxWarning: "\\400" is an invalid octal escape sequence ...

PySharp used to print the identical warning block 4 times for this
module-level statement (and more times in larger scripts).

This file only triggers the warning; the C# side
(TestSyntaxWarningOnceRegression) captures stderr and asserts the
occurrence count. The doubled backslashes in this docstring are
intentional so the docstring itself raises no warning.
"""

print("\400")
