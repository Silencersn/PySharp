"""
a capture name repeated inside one pattern (`case [a, a]:`) used
to be silently accepted - the pattern matched any two-element sequence with
the later binding shadowing the earlier one.

CPython 3.14 reference (Python/codegen.c codegen_pattern_helper_store_name):
every capture appends to a per-pattern stores list, and a repeat raises
"multiple assignments to name '<name>' in pattern". Each or-pattern
alternative carries its own stores list, so the same name in different
alternatives is legal, and names may repeat across separate cases.

:kind: test
"""


def expect_multiple_assignments(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError as e:
        message = str(e)
        assert "multiple assignments" in message, message


expect_multiple_assignments("match x:\n    case [a, a]:\n        pass")
expect_multiple_assignments("match x:\n    case {'a': b, 'c': b}:\n        pass")
expect_multiple_assignments("match x:\n    case a as a:\n        pass")
expect_multiple_assignments("match x:\n    case [b] as b:\n        pass")
expect_multiple_assignments(
    "class C:\n    __match_args__ = ('x', 'y')\nmatch x:\n    case C(a, a):\n        pass")
expect_multiple_assignments(
    "class C:\n    pass\nmatch x:\n    case C(x=b, y=b):\n        pass")
expect_multiple_assignments("match x:\n    case [a, [a]]:\n        pass")
expect_multiple_assignments("match x:\n    case a, a:\n        pass")
expect_multiple_assignments("match x:\n    case [a, *a]:\n        pass")


# the same name in different or-pattern alternatives binds one variable
def or_alternatives_same_name(v):
    match v:
        case [a] | (a,):
            return a
        case _:
            return "no"


assert or_alternatives_same_name([5]) == 5
assert or_alternatives_same_name((5,)) == 5
assert or_alternatives_same_name(5) == "no"


# names may repeat across separate cases
def reuse_across_cases(x):
    match x:
        case [a]:
            return a
        case [a, b]:
            return a + b
        case _:
            return "no"


assert reuse_across_cases([1]) == 1
assert reuse_across_cases([1, 2]) == 3
assert reuse_across_cases([1, 2, 3]) == "no"


# distinct names in one pattern, mapping rest included, keep working
def mapping_with_rest(v):
    match v:
        case {'a': b, **rest}:
            return (b, rest)
        case _:
            return "no"


assert mapping_with_rest({'a': 1, 'b': 2}) == (1, {'b': 2})
assert mapping_with_rest({'c': 1}) == "no"
