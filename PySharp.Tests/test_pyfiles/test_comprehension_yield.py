"""
yield inside the outermost iterable of a comprehension: the yield belongs
to the enclosing function and turns it into a generator, while every
position inside the comprehension body stays a SyntaxError.

:kind: test
"""


def check(src):
    try:
        compile(src, "<s>", "exec")
        return "OK"
    except SyntaxError as e:
        return str(e.msg)


def drive(gen, sent="A"):
    """next() then one send(); returns (produced, return-value)."""
    first = next(gen)
    try:
        gen.send(sent)
        raise AssertionError("generator did not finish")
    except StopIteration as stop:
        return first, stop.value


# --- the outermost iterable is evaluated in the enclosing scope ---

def g_list():
    return [x for x in [(yield 1)]]

assert type(g_list()) is type(i for i in [])
first, ret = drive(g_list())
assert first == 1 and ret == ["A"]

def g_set():
    return {x for x in [(yield 9)]}

first, ret = drive(g_set())
assert first == 9 and ret == {"A"}

def g_dict():
    return {k: k for k in [(yield 1)]}

first, ret = drive(g_dict())
assert first == 1 and ret == {"A": "A"}

def g_genexp():
    return list(x for x in [(yield 3)])

first, ret = drive(g_genexp())
assert first == 3 and ret == ["A"]

def g_yield_from():
    return [x for x in [(yield from [7])]]

assert next(g_yield_from()) == 7

def g_two_yields():
    yield 0
    return [x for x in [(yield 1)]]

gi = g_two_yields()
assert next(gi) == 0
assert next(gi) == 1
try:
    gi.send("B")
    raise AssertionError("generator did not finish")
except StopIteration as stop:
    assert stop.value == ["B"]

# a comprehension iterable nested inside another one stays outside every
# comprehension block
def g_nested():
    return [x for x in [[y for y in [(yield 1)]]]]

first, ret = drive(g_nested())
assert first == 1 and ret == [["A"]]

def g_nested_genexp():
    return [x for x in (y for y in [(yield 1)])]

first, ret = drive(g_nested_genexp())
assert first == 1 and ret == ["A"]

# --- a yield makes a lambda a generator the same way ---

def g_lambda():
    return lambda: (yield 1)

lam = g_lambda()
assert type(g_lambda()) is not type(i for i in [])  # g itself has no yield
# the lambda body is the yield expression itself, so the sent value is the
# lambda's implicit return
first, ret = drive(lam())
assert first == 1 and ret == "A"

def g_lambda_comp():
    return lambda: [x for x in [(yield 1)]]

first, ret = drive(g_lambda_comp()())
assert first == 1 and ret == ["A"]

# lambda defaults evaluate in the enclosing scope: the yield belongs to g
def g_lambda_default():
    return lambda a=[y for y in [(yield 1)]]: a

gi = g_lambda_default()
assert type(gi) is type(i for i in [])
first, ret = drive(gi)
assert first == 1 and callable(ret)

# --- positions inside the comprehension body stay illegal ---

assert check("def g():\n    return [(yield x) for x in range(3)]\n") \
    == "'yield' inside list comprehension"
assert check("def g():\n    return [x for x in range(2) if (yield x)]\n") \
    == "'yield' inside list comprehension"
assert check("def g():\n    return [x for x in [(yield 1)] for y in [(yield 2)]]\n") \
    == "'yield' inside list comprehension"
assert check("def g():\n    return {k: (yield k) for k in range(2)}\n") \
    == "'yield' inside dict comprehension"
assert check("def g():\n    return {x for x in range(2) if (yield x)}\n") \
    == "'yield' inside set comprehension"
assert check("def g():\n    return list(x for x in range(2) if (yield x))\n") \
    == "'yield' inside generator expression"
assert check("def g():\n    return [(yield from [1]) for x in range(2)]\n") \
    == "'yield' inside list comprehension"

# a genexp met inside a listcomp element blames the listcomp
assert check("def g():\n    return [(y for y in [(yield 1)]) for x in range(2)]\n") \
    == "'yield' inside list comprehension"

# module and class bodies are not functions
assert check("[x for x in [(yield 1)]]\n") == "'yield' outside function"
assert check("class C:\n    [x for x in [(yield 1)]]\n") == "'yield' outside function"
assert check("[x for x in range(2) if (yield x)]\n") == "'yield' inside list comprehension"

# --- async functions: 'yield from' is always illegal, and a value-returning
# statement reports the return rule before the yields inside it ---

assert check("async def g():\n    return [x for x in [(yield 1)]]\n") \
    == "'return' with value in async generator"
assert check("async def g():\n    return [(yield from [1])]\n") \
    == "'return' with value in async generator"
assert check("async def g():\n    x = [(yield from [1])]\n") \
    == "'yield from' inside async function"
assert check("async def g():\n    x = (yield from [1])\n    return 2\n") \
    == "'yield from' inside async function"
assert check("async def g():\n    yield 0\n    yield from [1]\n") \
    == "'yield from' inside async function"
