"""
Extended match statement tests - covers guard, sequence with *rest, literals, bool, None
"""
# Guard patterns
def match_guard(x):
    match x:
        case n if n > 0:
            return "positive"
        case n if n < 0:
            return "negative"
        case 0:
            return "zero"

assert match_guard(5) == "positive"
assert match_guard(-3) == "negative"
assert match_guard(0) == "zero"

# Sequence pattern with *rest unpacking
def match_seq(seq):
    match seq:
        case [a, b, *rest]:
            return (a, b, rest)
        case [a, *rest]:
            return (a, None, rest)
        case _:
            return None

r = match_seq([1, 2, 3, 4])
assert r == (1, 2, [3, 4])
r = match_seq([42])
assert r == (42, None, [])
r = match_seq([])
assert r is None

# Integer literal patterns
def match_int(x):
    match x:
        case 1:
            return "one"
        case 2:
            return "two"
        case _:
            return "other"

assert match_int(1) == "one"
assert match_int(99) == "other"

# Bool and None literal patterns
def match_const(x):
    match x:
        case True:
            return "true"
        case False:
            return "false"
        case None:
            return "nothing"
        case _:
            return "other"

assert match_const(True) == "true"
assert match_const(False) == "false"
assert match_const(None) == "nothing"
assert match_const(42) == "other"
assert match_const(1) == "other"
assert match_const(0) == "other"

print("test_match_extended passed")
