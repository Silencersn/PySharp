"""
Regression: a bare starred expression (`*a`) in an illegal position must
raise SyntaxError, not be silently accepted with the star stripped.

CPython 3.14 reference (Python/codegen.c:5301-5311): a Starred node that
reaches generic expression codegen is always an error - in store context
"starred assignment target must be in a list or tuple", otherwise
"can't use starred expression here". Legal starred forms (tuple/list
display elements, call arguments, assignment-target elements) are
consumed by their dedicated paths before that.

Each illegal form is checked through compile() so this file itself stays
valid: before the fix compile() succeeds (star silently stripped) and
the assertion fails; after the fix compile() must raise SyntaxError.
"""

def expect_syntax_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError:
        pass


# target side: starred assignment / for targets
expect_syntax_error("*a = [1]")
expect_syntax_error('*a = "xy"')
expect_syntax_error("for *a in [[1], [2]]:\n    pass")

# value side: bare starred in expression position
expect_syntax_error("a = [1, 2]\nx = *a")
expect_syntax_error("x: int = *[1]")

# --- guards: legal starred forms must keep working ---
a = [1, 2]
x = *a,
assert x == (1, 2)

*a, b = [1, 2]
assert a == [1] and b == 2

[*a] = [1]
assert a == [1]

# already rejected before the fix (parse-level), must stay rejected
try:
    compile("del *a", "<test>", "exec")
    assert False, "del *a should raise SyntaxError"
except SyntaxError:
    pass

print("test_starred_position_regression passed")
