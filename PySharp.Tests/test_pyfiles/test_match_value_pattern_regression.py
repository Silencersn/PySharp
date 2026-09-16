"""
Regression: dotted value patterns (`case Color.RED:`) used to put the parser
into an infinite ParseAttr <-> ParseNameOrAttr recursion (the token cursor was
reset before each re-entry), killing the process with an uncatchable .NET
stack overflow at compile time.

CPython 3.14 reference (Grammar/python.gram): value_pattern is an attr, and
name_or_attr (`attr | NAME`) is left-recursive - dotted names nest as
Attribute(Attribute(a, b), c), matching by equality. A bare name inside a
mapping pattern key stays a SyntaxError (only literals and dotted values are
valid keys).
"""

class Color:
    RED = 1
    GREEN = 2


def dispatch(c):
    match c:
        case Color.RED:
            return "red"
        case Color.GREEN:
            return "green"
        case _:
            return "other"


assert dispatch(1) == "red"
assert dispatch(2) == "green"
assert dispatch(3) == "other"


class Inner:
    DEEP = "deep"


class Outer:
    inner = Inner


def deep(v):
    match v:
        case Outer.inner.DEEP:
            return "hit"
        case _:
            return "miss"


assert deep("deep") == "hit"
assert deep("other") == "miss"


def or_pattern(v):
    match v:
        case Color.RED | Color.GREEN:
            return "rgb"
        case _:
            return "no"


assert or_pattern(1) == "rgb"
assert or_pattern(2) == "rgb"
assert or_pattern(3) == "no"


instance = Color()


def instance_attr(v):
    match v:
        case instance.RED:
            return "inst-red"
        case _:
            return "no"


assert instance_attr(1) == "inst-red"
assert instance_attr(2) == "no"


def mapping_key(v):
    match v:
        case {Color.RED: x}:
            return x
        case _:
            return "no"


assert mapping_key({1: "one"}) == "one"
assert mapping_key({2: "two"}) == "no"


def class_pattern_cls(v):
    match v:
        case Outer.inner():
            return "inner"
        case _:
            return "no"


assert class_pattern_cls(Inner()) == "inner"
assert class_pattern_cls(1) == "no"


def mixed_with_captures(v):
    match v:
        case Color.RED if v > 0:
            return "red-positive"
        case [Color.RED, b]:
            return b
        case _:
            return "no"


assert mixed_with_captures(1) == "red-positive"
assert mixed_with_captures([1, 2]) == 2
assert mixed_with_captures([3, 2]) == "no"


def expect_syntax_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError:
        pass


# a bare name is a capture, not a value - invalid as a mapping pattern key
expect_syntax_error("match x:\n    case {a: 1}:\n        pass")
