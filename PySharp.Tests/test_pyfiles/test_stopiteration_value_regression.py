"""
Regression: StopIteration must expose the value attribute like CPython -
a value member initialized from args[0] (or None) at construction,
settable without touching args, deletable (reading None afterwards,
without falling back to args). PySharp used to raise AttributeError on
every .value read, so generator return values were unreadable at Python
level.

CPython member semantics pinned here: e.value = 9 leaves args untouched;
deleting value reads None even when args is non-empty.
"""


def gen():
    yield 1
    return 77


def gen_plain():
    yield 1


# red cases: generator return values readable via .value
g = gen()
next(g)
try:
    next(g)
    assert False, "expected StopIteration"
except StopIteration as e:
    assert e.value == 77, e.value
    assert e.args == (77,), e.args

g2 = gen_plain()
next(g2)
try:
    next(g2)
    assert False, "expected StopIteration"
except StopIteration as e:
    assert e.value is None, e.value

# direct construction
assert StopIteration(77).value == 77
assert StopIteration().value is None


# user iterator __next__ raise path
class It:
    def __iter__(self):
        return self

    def __next__(self):
        raise StopIteration(42)


it = It()
try:
    next(it)
    assert False, "expected StopIteration"
except StopIteration as e:
    assert e.value == 42, e.value
    assert e.args == (42,), e.args


# CPython member semantics: set never touches args
e1 = StopIteration(5)
e1.value = 9
assert e1.value == 9
assert e1.args == (5,), e1.args

e2 = StopIteration()
e2.value = 42
assert e2.value == 42
assert e2.args == (), e2.args


# del reads None without args fallback
e3 = StopIteration(5)
del e3.value
assert e3.value is None


# guards: iteration and yield from passthrough unaffected
assert list(gen_plain()) == [1]


def outer():
    result = yield from gen()
    yield result


o = outer()
assert next(o) == 1
assert next(o) == 77

print("test_stopiteration_value_regression passed")
