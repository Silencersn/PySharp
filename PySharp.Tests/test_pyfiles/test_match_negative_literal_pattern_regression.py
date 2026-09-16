"""
Regression: negative numeric literals in match case patterns (`case -1:`,
`case -1.5:`) used to be rejected with a plain SyntaxError, while PEP 634
allows signed numbers (minus only) as literal patterns.

CPython 3.14 reference (Grammar/python.gram):
    literal_pattern: signed_number !('+' | '-') | complex_number | strings ...
    signed_number: NUMBER | '-' NUMBER

`+1` stays invalid (PEP 634 allows only the minus sign), and a minus before
a non-number (name, string) or a non-imaginary second operand stays invalid.
"""


def neg_int(v):
    match v:
        case -1:
            return "hit"
        case _:
            return "no"


assert neg_int(-1) == "hit"
assert neg_int(1) == "no"
assert neg_int(-2) == "no"


def neg_float(v):
    match v:
        case -1.5:
            return "hit"
        case _:
            return "no"


assert neg_float(-1.5) == "hit"
assert neg_float(1.5) == "no"
assert neg_float(-2.5) == "no"


def or_with_negatives(v):
    match v:
        case 0 | -2 | 3.25:
            return "hit"
        case _:
            return "no"


assert or_with_negatives(0) == "hit"
assert or_with_negatives(-2) == "hit"
assert or_with_negatives(3.25) == "hit"
assert or_with_negatives(2) == "no"


def neg_complex(v):
    match v:
        case -1+2j:
            return "c"
        case -0.0:
            return "neg-zero"
        case _:
            return "no"


assert neg_complex(complex(-1, 2)) == "c"
assert neg_complex(1 + 2j) == "no"
assert neg_complex(0.0) == "neg-zero"


def neg_mapping_key(v):
    match v:
        case {-1: x}:
            return x
        case _:
            return "no"


assert neg_mapping_key({-1: "a"}) == "a"
assert neg_mapping_key({1: "b"}) == "no"


def neg_with_space(v):
    match v:
        case - 1:
            return "hit"
        case _:
            return "no"


assert neg_with_space(-1) == "hit"


def expect_syntax_error(src):
    try:
        compile(src, "<test>", "exec")
        assert False, "should raise SyntaxError: " + repr(src)
    except SyntaxError:
        pass


# PEP 634 allows only the minus sign
expect_syntax_error("match x:\n    case +1:\n        pass")
# minus before a non-number
expect_syntax_error("match x:\n    case -y:\n        pass")
expect_syntax_error("match x:\n    case -'s':\n        pass")
# complex literal needs an imaginary second operand
expect_syntax_error("match x:\n    case -1 - 2:\n        pass")
