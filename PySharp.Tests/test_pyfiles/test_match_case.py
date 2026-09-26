"""
Python 3.10+ match-case statement tests

:kind: test
"""

def test_match(x):
    match x:
        case 1:
            return "one"
        case 2 | 3:
            return "two or three"
        case "test":
            return "string"
        case _:
            return "default"

assert test_match(1) == "one"
assert test_match(2) == "two or three"
assert test_match(3) == "two or three"
assert test_match("test") == "string"
assert test_match(999) == "default"

# Sequence pattern matching
def match_sequence(lst):
    match lst:
        case [1, 2, 3]:
            return "123"
        case [1, *rest]:
            return "one and rest"
        case []:
            return "empty"
        case _:
            return "other"

assert match_sequence([1, 2, 3]) == "123"
assert match_sequence([1, 4, 5, 6]) == "one and rest"
assert match_sequence([]) == "empty"
assert match_sequence([2, 3, 4]) == "other"

print("test_match_case passed")
