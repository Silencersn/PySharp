"""
a class pattern with positional sub-patterns against a class
without __match_args__ raises the arity TypeError
"<Name>() accepts 0 positional sub-patterns (N given)" like CPython
MatchClass. The old implementation let the __match_args__ attribute
lookup error escape as AttributeError, so `except AttributeError` would
misreport a mistaken match pattern as an attribute bug.

CPython 3.14 reference (_PyEval_MatchClass): a missing __match_args__
counts as an empty tuple; the allowed count is 1 for MATCH_SELF types;
the plural marker is omitted when the allowed count is exactly 1.

:kind: test
"""

class Plain:
    pass

class Two:
    __match_args__ = ('a',)

# missing __match_args__ -> arity TypeError (not AttributeError)
def f_plain(v):
    match v:
        case Plain(1):
            return 'matched'
        case _:
            return 'no'

try:
    f_plain(Plain())
    assert False, "TypeError expected"
except AttributeError:
    assert False, "AttributeError leaked"
except TypeError as e:
    assert str(e) == "Plain() accepts 0 positional sub-patterns (1 given)"

# built-in exception classes behave the same
def f_exc(v):
    match v:
        case KeyError(k):
            return 'matched'
        case _:
            return 'no'

try:
    f_exc(KeyError('k'))
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "KeyError() accepts 0 positional sub-patterns (1 given)"

# bare class pattern and keyword sub-patterns still work
def f_bare(v):
    match v:
        case Plain():
            return 'matched'
        case _:
            return 'no'

assert f_bare(Plain()) == 'matched'
assert f_bare(42) == 'no'

# existing arity checks: singular for one allowed, plural otherwise
def f_two(v):
    match v:
        case Two(1, 2):
            return 'matched'
        case _:
            return 'no'

try:
    f_two(Two())
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "Two() accepts 1 positional sub-pattern (2 given)"

# MATCH_SELF-style built-in types keep the 1-argument form
def f_str(v):
    match v:
        case str(s):
            return s
        case _:
            return 'no'

assert f_str('hi') == 'hi'

def f_str_two(v):
    match v:
        case str(1, 2):
            return 'matched'
        case _:
            return 'no'

try:
    f_str_two('hi')
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "str() accepts 1 positional sub-pattern (2 given)"

# a non-tuple __match_args__ is still the tuple TypeError
class Bad:
    __match_args__ = 'oops'

def f_bad(v):
    match v:
        case Bad(1):
            return 'matched'
        case _:
            return 'no'

try:
    f_bad(Bad())
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "Bad.__match_args__ must be a tuple (got str)"

# a missing subject attribute inside a matched pattern is still no-match
class Hidden:
    __match_args__ = ('y',)

def f_hidden(v):
    match v:
        case Hidden(1):
            return 'matched'
        case _:
            return 'no'

assert f_hidden(Hidden()) == 'no'

print("test_match_missing_match_args passed")
