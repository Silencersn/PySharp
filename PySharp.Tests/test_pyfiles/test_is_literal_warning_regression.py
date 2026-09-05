"""
Regression: `is` / `is not` with a constant literal operand must emit a
SyntaxWarning suggesting the value comparison operator, like CPython.
PySharp used to be completely silent (evaluation results already match).

CPython 3.14 reference (Python/codegen.c codegen_check_compare): either
operand being a constant literal warns - "is" with '...' literal.
Did you mean "=="? / "is not" ... Did you mean "!="? - except for the
True/False/None singletons, and non-constant expression nodes.

This file only triggers the warnings; the C# side
(TestIsLiteralWarningRegression) captures stderr and asserts:
6 x 'is' warnings ("==") + 1 x 'is not' warning ("!="), while the
singleton forms contribute none.
"""

x = 5

# literal operands: must warn (6 x "==")
print(x is 5)
print(x is 5.0)
print(x is 5.5)
print(x is "s")
print(x is b"b")
print(5 is 5)

# "is not" variant: must warn with "!=" (1 x)
print(x is not 5)

# singletons: must NOT warn (guards; included in no count)
print(x is True)
print(x is False)
print(x is None)

print("test_is_literal_warning_regression passed")
