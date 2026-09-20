# Argument binding errors. CPython's initialize_locals (Python/ceval.c) has a
# message for every way a call can fail, and they are all built from the
# failing parameter set: the missing parameters, the positional overflow, a
# repeated argument, an unexpected or positional-only keyword, each naming the
# callable by its (mutable) __qualname__. Binding keywords is also what
# happens first, so an unexpected keyword outranks a missing parameter and a
# positional overflow outranks a missing keyword-only one. The messages used
# to come back empty.
def type_error(fn):
    try:
        fn()
    except TypeError as e:
        text = str(e)
        assert text, "the binding TypeError must describe the failure"
        return text
    raise AssertionError("expected a TypeError")


# --- missing parameters, named one by one ---

def f1(a, b=2, *args, c, d=4, **kw):
    return (a, b, args, c, d, kw)

assert type_error(lambda: f1()) == "f1() missing 1 required positional argument: 'a'"
assert type_error(lambda: f1(1, 2)) == "f1() missing 1 required keyword-only argument: 'c'"
assert type_error(lambda: f1(1, 2, zz=3)) == "f1() missing 1 required keyword-only argument: 'c'"


def g0(a, b, c):
    return (a, b, c)

# format_missing spells the list out: "and" for two, an Oxford comma beyond
assert type_error(lambda: g0()) == "g0() missing 3 required positional arguments: 'a', 'b', and 'c'"


def g1(a, b, c=3):
    return (a, b, c)

assert type_error(lambda: g1()) == "g1() missing 2 required positional arguments: 'a' and 'b'"


def h(a, *args, x, y):
    return (a, args, x, y)

assert type_error(lambda: h(1)) == "h() missing 2 required keyword-only arguments: 'x' and 'y'"


# --- positional overflow: the arity in the message depends on the defaults ---

def t2(a, b):
    return (a, b)

assert type_error(lambda: t2(1, 2, 3)) == "t2() takes 2 positional arguments but 3 were given"


def t0():
    return None

assert type_error(lambda: t0(1)) == "t0() takes 0 positional arguments but 1 was given"


def t1(a=1):
    return a

assert type_error(lambda: t1(1, 2)) == "t1() takes from 0 to 1 positional arguments but 2 were given"
assert type_error(lambda: t1(1, 2, 3)) == "t1() takes from 0 to 1 positional arguments but 3 were given"


def t3(a, b, c, *, d):
    return (a, b, c, d)

assert type_error(lambda: t3(1, 2, 3, 4)) == "t3() takes 3 positional arguments but 4 were given"


def order(a, *, b):
    return (a, b)

# the overflow is reported before the keyword-only parameter it left behind
assert type_error(lambda: order(1, 2, 3)) == "order() takes 1 positional argument but 3 were given"


def order2(a, b=1, *, c):
    return (a, b, c)

assert type_error(lambda: order2(1, 2, 3, 4)) == "order2() takes from 1 to 2 positional arguments but 4 were given"


def star_swallows(a, *args):
    return (a, args)

assert star_swallows(1, 2, 3) == (1, (2, 3))


# --- repeated and unexpected keywords ---

def m3(a, b):
    return (a, b)

assert type_error(lambda: m3(1, 2, b=3)) == "m3() got multiple values for argument 'b'"
assert type_error(lambda: m3(1, a=5, b=2)) == "m3() got multiple values for argument 'a'"


def nokw(a):
    return a

assert type_error(lambda: nokw(1, q=2)) == "nokw() got an unexpected keyword argument 'q'"


def od(a, b, *, c):
    return (a, b, c)

# the whole keyword set is bound before the missing parameters are reported
assert type_error(lambda: od(1, zz=2)) == "od() got an unexpected keyword argument 'zz'"
assert type_error(lambda: od(1, c=3)) == "od() missing 1 required positional argument: 'b'"


def onlystar(*args):
    return args

assert onlystar() == ()
assert type_error(lambda: onlystar(q=1)) == "onlystar() got an unexpected keyword argument 'q'"


def onlykw(**kw):
    return kw

assert onlykw(a=1) == {"a": 1}
assert type_error(lambda: onlykw(1)) == "onlykw() takes 0 positional arguments but 1 was given"


# --- positional-only parameters passed by name ---

def p2(a, /, b, *, c):
    return (a, b, c)

assert type_error(lambda: p2(a=1, b=2, c=3)) == "p2() got some positional-only arguments passed as keyword arguments: 'a'"


def p2b(a, b, /):
    return (a, b)

assert type_error(lambda: p2b(a=1, b=2)) == "p2b() got some positional-only arguments passed as keyword arguments: 'a, b'"


def po(a, /, b):
    return (a, b)

assert type_error(lambda: po(a=1)) == "po() got some positional-only arguments passed as keyword arguments: 'a'"


def kwa(a, *args, c):
    return (a, args, c)

# a keyword-only name reaching *args is not an overflow, it is still missing
assert type_error(lambda: kwa(1, 2, 3)) == "kwa() missing 1 required keyword-only argument: 'c'"


# --- the callable is named by its __qualname__ ---

class Point:
    def __init__(self, x, y):
        self.x = x
        self.y = y


assert type_error(lambda: Point(1, 2, 3)) == "Point.__init__() takes 3 positional arguments but 4 were given"


class CM:
    @classmethod
    def cm(cls, a):
        return (cls, a)


assert type_error(lambda: CM.cm()) == "CM.cm() missing 1 required positional argument: 'a'"
assert type_error(lambda: CM.cm.__func__()) == "CM.cm() missing 2 required positional arguments: 'cls' and 'a'"


def renamed(a):
    return a


renamed.__qualname__ = "Renamed.renamed"
assert type_error(lambda: renamed()) == "Renamed.renamed() missing 1 required positional argument: 'a'"


def outer():
    def inner(a):
        return a
    return inner


assert type_error(lambda: outer()()) == "outer.<locals>.inner() missing 1 required positional argument: 'a'"


# --- an unknown keyword is matched against the parameters that could take it ---

def suggest(alpha, beta):
    return (alpha, beta)

assert type_error(lambda: suggest(alpah=1)) == "suggest() got an unexpected keyword argument 'alpah'. Did you mean 'alpha'?"
assert type_error(lambda: suggest(bet=1)) == "suggest() got an unexpected keyword argument 'bet'. Did you mean 'beta'?"
assert type_error(lambda: suggest(zzzzz=1)) == "suggest() got an unexpected keyword argument 'zzzzz'"


def sug_case(Value):
    return Value


# a case flip is cheaper than a different character
assert type_error(lambda: sug_case(value=1)) == "sug_case() got an unexpected keyword argument 'value'. Did you mean 'Value'?"


def kwonly_sug(*, alpha):
    return alpha


assert type_error(lambda: kwonly_sug(alpah=1)) == "kwonly_sug() got an unexpected keyword argument 'alpah'. Did you mean 'alpha'?"


def posonly_sug(alpha, /):
    return alpha


# a positional-only parameter is not a candidate, and a typo is not a clash
assert type_error(lambda: posonly_sug(alpah=1)) == "posonly_sug() got an unexpected keyword argument 'alpah'"


# --- a duplicate keyword from a ** mapping merge names the callable in full ---

def dq(x, y):
    return x


module = dq.__module__
assert type_error(lambda: dq(**{"x": 1}, **{"x": 2})) == module + ".dq() got multiple values for keyword argument 'x'"
assert type_error(lambda: dq(**{"x": 1, "y": 2}, x=3)) == module + ".dq() got multiple values for keyword argument 'x'"


class DQ:
    def m(self, x):
        return x


assert type_error(lambda: DQ().m(**{"x": 1}, **{"x": 2})) == module + ".DQ.m() got multiple values for keyword argument 'x'"


# --- binding itself still fills every parameter shape the same way ---

def combo(a, b=1, /, c=2, *args, d, e=5, **kw):
    return (a, b, c, args, d, e, kw)


assert combo(1, 2, 3, 4, 5, d=6, zz=7) == (1, 2, 3, (4, 5), 6, 5, {"zz": 7})
assert combo(1, d=6) == (1, 1, 2, (), 6, 5, {})


def star_kw(a, *args, b, c=3, **kw):
    return (a, args, b, c, kw)


# a bound keyword keeps its value against the keyword-only defaults
assert star_kw(1, 2, 3, b=4, z=5) == (1, (2, 3), 4, 3, {"z": 5})
assert star_kw(1, b=4) == (1, (), 4, 3, {})


# --- range parses its own arguments: positional only, through __index__ ---

assert type_error(lambda: range()) == "range expected at least 1 argument, got 0"
assert type_error(lambda: range(1, 2, 3, 4)) == "range expected at most 3 arguments, got 4"
assert type_error(lambda: range(zz=1)) == "range() takes no keyword arguments"
assert type_error(lambda: range("a")) == "'str' object cannot be interpreted as an integer"
assert type_error(lambda: range(1, "a")) == "'str' object cannot be interpreted as an integer"


class Indexable:
    def __index__(self):
        return 3


assert list(range(Indexable())) == [0, 1, 2]
assert list(range(3)) == [0, 1, 2]
