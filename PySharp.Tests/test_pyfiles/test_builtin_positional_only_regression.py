# Built-in callables whose CPython signature has no keyword support at all must
# reject every keyword argument, while the ones that do declare keyword
# parameters keep accepting them. The declarations used to leave trailing
# parameters keyword-capable (str.count/find/index/rfind/rindex/startswith/
# endswith, any/all, the generator/coroutine/async-generator send family, file
# and stdio read/readline/readlines/write/seek), and round() was the opposite
# case — its number/ndigits are positional-or-keyword in CPython but were
# declared positional-only.
def type_error(fn):
    try:
        fn()
    except TypeError as e:
        text = str(e)
        assert text, "the binding TypeError must describe the failure"
        return text
    raise AssertionError("expected a TypeError")


def gen():
    yield 1


async def coro():
    return 1


async def agen():
    yield 1


# --- str search family: sub/prefix/suffix and start/end are positional-only ---

for call in [
    lambda: "aaa".count("a", start=1),
    lambda: "aaa".count("a", end=2),
    lambda: "abcabc".find("b", end=3),
    lambda: "abcabc".rfind("b", start=1),
    lambda: "abcabc".index("b", start=1),
    lambda: "abcabc".rindex("b", end=3),
    lambda: "abc".startswith("a", start=0),
    lambda: "abc".endswith("c", end=3),
]:
    type_error(call)

# the positional forms keep their window semantics
assert "aaa".count("a", 1) == 2
assert "abcabc".find("b", 1, 3) == 1
assert "abcabc".rfind("b", 1, 3) == 1
assert "abcabc".index("b", 1, 3) == 1
assert "abcabc".rindex("b", 1, 3) == 1
assert "abc".startswith("a", 0, 1) is True
assert "abc".endswith("c", 0, 3) is True
assert "abc".startswith(("x", "a")) is True

# --- any/all take one positional-only iterable ---

type_error(lambda: any(iterable=[1]))
type_error(lambda: all(iterable=[1]))
assert any([0, 1]) is True
assert all([1, 1]) is True

# --- round: number/ndigits are callable by name ---

assert round(1.5) == 2
assert round(1.55, 1) == 1.6
assert round(number=1.5) == 2
assert round(1.55, ndigits=1) == 1.6
assert round(number=1.55, ndigits=1) == 1.6

# --- generator / coroutine / async generator: send family is positional-only ---

g = gen()
assert next(g) == 1
type_error(lambda: g.send(value=5))
assert gen().send(None) == 1

c1 = coro()
type_error(lambda: c1.send(value=1))
c1.close()
c2 = coro()
type_error(lambda: c2.throw(value=ValueError))
c2.close()

type_error(lambda: agen().asend(value=1))
type_error(lambda: agen().athrow(value=ValueError))

# --- file object: no keyword arguments on the io methods ---

open("_test_posonly.txt", "w").write("abc\n")
f = open("_test_posonly.txt")
type_error(lambda: f.read(size=1))
type_error(lambda: f.readline(size=1))
type_error(lambda: f.readlines(hint=1))
type_error(lambda: f.seek(offset=0))
assert f.read(1) == "a"
assert f.seek(1, 0) == 1
assert f.read() == "bc\n"
assert f.readline() == ""
f.close()

a = open("_test_posonly.txt", "a")
type_error(lambda: a.write(data="x"))
assert a.write("x") == 1
a.close()

# --- stdout: the write argument is positional-only too ---

import sys

type_error(lambda: sys.stdout.write(data="x"))
