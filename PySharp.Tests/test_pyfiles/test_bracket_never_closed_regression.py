"""
Regression: the lexer must track every open bracket on a stack and
report the innermost one still open at EOF (CPython raises
SyntaxError: 'X' was never closed from parenstack[level-1]). The
inputs below used to reach the parser, which answered with misleading
messages like "positional argument follows keyword argument" or a
plain "invalid syntax".

Also verifies the sibling checks from the same stack: a closing
bracket that pairs with a different opener, and the pre-existing
"unmatched 'X'" for a close with an empty stack.
"""

failures = []


def check(source, expected):
    try:
        compile(source, "<test>", "exec")
    except SyntaxError as e:
        if expected not in str(e):
            failures.append(repr(source) + ": expected " + repr(expected) + " in " + repr(str(e)))
        return
    failures.append(repr(source) + ": no SyntaxError raised")


# red cases: unclosed brackets must be reported by the lexer
check("f(f(1)", "'(' was never closed")
check("(1", "'(' was never closed")
check("x = [1", "'[' was never closed")
check("x = {1", "'{' was never closed")
check("x = (1 + (2)", "'(' was never closed")
check("def f(\n  pass", "'(' was never closed")
check("f(1\n(2", "'(' was never closed")
check("f(1", "'(' was never closed")

# the innermost still-open bracket wins
check("([1", "'[' was never closed")
check("f(x[1", "'[' was never closed")

# f-string replacement fields share the same stack
check('f"{1', "'{' was never closed")
check('f"{(1', "'(' was never closed")

# mismatched closing brackets
check("f(1]", "closing parenthesis ']' does not match opening parenthesis '('")
check("x = [1)", "closing parenthesis ')' does not match opening parenthesis '['")
check("f(1\n]", "closing parenthesis ']' does not match opening parenthesis '(' on line 1")
check('f"{(1}"', "closing parenthesis '}' does not match opening parenthesis '('")

# a close with an empty stack stays unmatched
check("f)", "unmatched ')'")
check("x = 1}", "unmatched '}'")

# guards: balanced code is unaffected
def identity(value):
    return value


assert identity(identity(1)) == 1
assert eval('f"{1 + 1}"') == "2"
assert eval('f"{ {1: 2}[1] }"') == "2"
assert eval('f"{a[0]:>2}"', {"a": [7]}) == " 7"
compile("x = [(1, {2: 3})]\n", "<test>", "exec")

if failures:
    raise AssertionError("\n".join(failures))

print("ok")
