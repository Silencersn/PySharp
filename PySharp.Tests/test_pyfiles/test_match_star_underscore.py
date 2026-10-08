"""
Match patterns with the anonymous starred pattern '*'_' at any position

:kind: test
"""
# '*'_' at the end
def star_last(seq):
    match seq:
        case [x, *_]:
            return (x,)
        case _:
            return None

assert star_last([1, 2, 3]) == (1,)
assert star_last([1]) == (1,)
assert star_last([]) is None

# '*'_' at the start
def star_first(seq):
    match seq:
        case [*_, x]:
            return x
        case _:
            return None

assert star_first([1, 2, 3]) == 3
assert star_first([3]) == 3
assert star_first([]) is None

# '*'_' in the middle
def star_middle(seq):
    match seq:
        case [x, *_, y]:
            return (x, y)
        case _:
            return None

assert star_middle([1, 2, 3, 4]) == (1, 4)
assert star_middle([1, 4]) == (1, 4)
assert star_middle([1]) is None

# minimum length: one element fewer than the pattern is enough
match [1, 2]:
    case [a, *_, b]:
        r = (a, b)
assert r == (1, 2)

# empty subject satisfies a pattern of only '*'_'
match []:
    case [*_]:
        r = "empty"
assert r == "empty"

# anonymous star collects without binding: the name stays unset
match [1, 2, 3]:
    case [1, *_]:
        r = "matched"
assert r == "matched"

# tuple sequence pattern
def star_tuple(seq):
    match seq:
        case (x, *_, y):
            return (x, y)
        case _:
            return None

assert star_tuple((1, 2, 3)) == (1, 3)

# open sequence pattern (no brackets)
def star_open(seq):
    match seq:
        case *_, y:
            return y
        case _:
            return None

assert star_open([1, 2, 3]) == 3

# sequence check still applies to a pattern with only '*'_'
r = "unmatched"
match {1: "a"}:
    case [*_,]:
        r = "matched"
assert r == "unmatched"
