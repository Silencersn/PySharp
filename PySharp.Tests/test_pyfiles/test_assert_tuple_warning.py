"""
`assert (tuple literal display)` emits the compile-time
SyntaxWarning CPython emits in codegen_assert. Any non-empty tuple
literal display as the assert test warns once with
"assertion is always true, perhaps remove parentheses?" regardless of
element values or constness (the classic `assert (cond, "msg")`
mistake); a variable-bound tuple, a parenthesized non-tuple, and an
empty tuple `()` (always false, not always true) do not warn, and the
runtime behavior of every form is unchanged.

CPython 3.14 reference (Python/codegen.c codegen_assert: Tuple_kind
with len > 0, or Constant_kind holding a non-empty tuple).

:kind: test
"""

import warnings


def caught_messages(code):
    with warnings.catch_warnings(record=True) as w:
        warnings.simplefilter("always")
        try:
            exec(code)
        except AssertionError:
            pass
        return [(item.category.__name__, str(item.message)) for item in w]


# tuple literal display warns, whatever the elements
warned = caught_messages("assert (False, 'msg')")
assert warned == [("SyntaxWarning", "assertion is always true, perhaps remove parentheses?")], warned

assert len(caught_messages("assert (True, 1)")) == 1
assert len(caught_messages("assert (1, 2)")) == 1
assert len(caught_messages("assert ((False, 'm'))")) == 1, "double parentheses still a tuple display"
assert len(caught_messages("assert ((False, 'm'), 'm2')")) == 1, "test tuple with a msg part still warns"
assert len(caught_messages("x = 1\nassert (x, 'msg')")) == 1, "non-constant elements still warn"
assert len(caught_messages("assert ((False, 'inner'),)")) == 1, "single-element tuple warns"

# runtime behavior unchanged: no AssertionError for a truthy tuple
exec("assert (1, 2)\nprint('executed')")

# non-tuple tests stay silent
assert caught_messages("x = (False, 1)\nassert x") == [], "variable-bound tuple must not warn"
assert caught_messages("assert (1)") == [], "parenthesized non-tuple must not warn"
assert caught_messages("assert 1, 'msg'") == [], "plain test with msg must not warn"

# the empty tuple is always FALSE, so codegen_assert does not warn
try:
    exec("assert ()")
    raise SystemExit("empty tuple assert must raise")
except AssertionError:
    pass
assert caught_messages("assert ()") == [], "empty tuple must not warn"

# each offending statement warns once
assert len(caught_messages("assert (1, 2)\nassert (3, 4)")) == 2

print("assert tuple literal warning passed")
