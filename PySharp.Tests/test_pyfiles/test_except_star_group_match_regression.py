"""
Regression: except* rejects a group match type, and split matches node-first.

PEP 654 forbids catching a group with except*. CPython's CHECK_EG_MATCH runs
_PyEval_CheckExceptStarTypeValid (Python/ceval.c), which validates the type as
any except does and then rejects it when it is — or when its tuple holds — a
BaseExceptionGroup subclass. The rejection is a runtime check: a clause that
the try never reaches is never validated, so `try: pass / except* ExceptionGroup`
stays accepted.

BaseExceptionGroup.split mirrors exceptiongroup_split_recursive
(Objects/exceptions.c): the node itself is tested before its children, so a
condition matching the group yields that very group — not a derived subset —
and only a non-matching group descends.
"""

EG_MESSAGE = ("catching ExceptionGroup with except* is not allowed. "
              "Use except instead.")
BASE_MESSAGE = ("catching classes that do not inherit from BaseException "
                "is not allowed")
CONDITION_MESSAGE = ("expected an exception type, a tuple of exception types, "
                     "or a callable (other than a class)")


class MyGroup(ExceptionGroup):
    pass


def raise_group():
    raise ExceptionGroup("g", [ValueError("v")])


def raise_bare():
    raise ValueError("v")


def raise_nothing():
    pass


def catch(clause_type, raiser):
    """Return the except* clause's record; the clause cannot return itself."""
    out = []
    try:
        raiser()
    except* clause_type:
        out.append("caught")
    return out


def assert_rejected(clause_type, raiser, message):
    try:
        catch(clause_type, raiser)
    except TypeError as e:
        assert str(e) == message, "unexpected message: %r" % (str(e),)
    else:
        assert False, "TypeError expected for %r" % (clause_type,)


# a group type, a tuple holding one, and any subclass are all rejected
for clause_type in (ExceptionGroup, BaseExceptionGroup, MyGroup,
                    (ExceptionGroup,), (ValueError, ExceptionGroup),
                    (BaseExceptionGroup, ValueError)):
    assert_rejected(clause_type, raise_group, EG_MESSAGE)

# the rejection does not need a group to be raised, and it does not wait for
# the rest to be exhausted either
assert_rejected(ExceptionGroup, raise_bare, EG_MESSAGE)


def exhausted_rest():
    try:
        raise ExceptionGroup("g", [ValueError("v")])
    except* ValueError:
        pass
    except* ExceptionGroup:
        pass


try:
    exhausted_rest()
except TypeError as e:
    assert str(e) == EG_MESSAGE, "unexpected message: %r" % (str(e),)
else:
    assert False, "TypeError expected for an exhausted rest"

# the type validation except shares runs first, so a group type never masks it
assert_rejected((int, ExceptionGroup), raise_bare, BASE_MESSAGE)
assert_rejected(int, raise_bare, BASE_MESSAGE)

# a clause the try never reaches is not validated
assert catch(ExceptionGroup, raise_nothing) == []

# non-group types keep matching
assert catch(ValueError, raise_group) == ["caught"]
assert catch(Exception, raise_group) == ["caught"]


# split tests the node itself before descending
g = ExceptionGroup("o", [ValueError("v"), TypeError("t")])
for condition in (Exception, ExceptionGroup, BaseException):
    match, rest = g.split(condition)
    assert match is g, "a matching group is its own match"
    assert rest is None

# a partial match derives both subsets from the same group
match, rest = g.split(ValueError)
assert match is not g and rest is not g
assert repr(match) == "ExceptionGroup('o', [ValueError('v')])"
assert repr(rest) == "ExceptionGroup('o', [TypeError('t')])"

# a group condition matches a group node rather than its leaves
inner = ExceptionGroup("i", [ValueError("v")])
g2 = ExceptionGroup("o", [ValueError("v"), inner])
match, rest = g2.split(ExceptionGroup)
assert match is g2 and rest is None
match, rest = g2.split(ValueError)
assert repr(match) == "ExceptionGroup('o', [ValueError('v'), ExceptionGroup('i', [ValueError('v')])])"
assert rest is None

g3 = ExceptionGroup("o", [ExceptionGroup("i", [ValueError("v")])])
match, rest = g3.split(ExceptionGroup)
assert match is g3 and rest is None
match, rest = g3.split(ValueError)
assert repr(match) == "ExceptionGroup('o', [ExceptionGroup('i', [ValueError('v')])])"
assert rest is None

# no match at all leaves the whole group as the rest
match, rest = g.split(KeyError)
assert match is None
assert rest is not g
assert repr(rest) == "ExceptionGroup('o', [ValueError('v'), TypeError('t')])"

# a callable condition is still a predicate
match, rest = g3.split(lambda e: isinstance(e, ValueError))
assert match is not None and rest is None
mixed = ExceptionGroup("o", [ValueError("v"), ExceptionGroup("i", [TypeError("t")])])
match, rest = mixed.split(lambda e: isinstance(e, ValueError))
assert repr(match) == "ExceptionGroup('o', [ValueError('v')])"
assert repr(rest) == "ExceptionGroup('o', [ExceptionGroup('i', [TypeError('t')])])"

# the condition must be a group type, a tuple of exception types, or a
# callable that is not a class
for condition in (123, None, "s", (ValueError, 123)):
    try:
        g.split(condition)
    except TypeError as e:
        assert str(e) == CONDITION_MESSAGE, "unexpected message: %r" % (str(e),)
    else:
        assert False, "TypeError expected for %r" % (condition,)

print("test_except_star_group_match_regression passed")
