"""
Regression: compiling deeply nested call expressions must stay roughly
linear, not exponential. PySharp used to blow up exponentially on
f(f(...)) chains (+2 levels ~= x5; 24 levels > 60s; CPython: instant),
making valid but deep builder/wrapper chains unusable.

CPython 3.14 parses any nesting depth instantly (PEG parser with full
memoization, Parser/pegen.c).

This file only provides the source; the C# side
(TestNestedCallCompileTimeRegression) measures the compile+run wall
time around RunModule and asserts a threshold that the exponential
parser cannot meet (~24s measured at this depth) but a linear parser
passes with a huge margin.

The nested list display form ([[[...]]]) shares the same parser path
and explodes even harder per level; it is intentionally not included
so this test stays fast to fail.
"""

def f(v):
    return v
x = f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(f(1))))))))))))))))))))))
print("nested call compile ok")
