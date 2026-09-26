"""
`not` in a short-circuiting test must not be folded into the jump.

The bytecode builder used to infer from the previously emitted instruction
alone that (a) a trailing UNARY_NOT belonged to the whole test expression and
could be replaced by an inverted jump, and (b) a TO_BOOL directly in front of
that jump was redundant. Both inferences are unsound when the test is an
`and`/`or` whose last operand is `not`: the operand value stays on the stack as
the expression result, so on the short-circuit path the jump ended up testing
an un-negated value — which need not even be a bool.

Two user-visible symptoms followed: an inverted branch when the short-circuit
value was a bool, and an unhandled InvalidCastException (the VM casts the jump
operand to PyBoolObject) when it was any other truthy object.

CPython 3.14 reference (Python/codegen.c): codegen_jump_if recurses into `not`
with the inverted sense (1889-1891) and otherwise always emits TO_BOOL before
POP_JUMP_IF_TRUE/FALSE (1971-1975); it never rewrites already emitted jumps.

:kind: test
"""

seen = []


def note(label):
    seen.append(label)


def last():
    return seen[-1] if seen else None


# ============================================================
# 1. Trailing `not` over a bool short-circuit value: branch direction
# ============================================================

x = 0

if True or not x:
    note("if-bool")
assert last() == "if-bool", last()

assert (1 if (True or not x) else 2) == 1

assert [i for i in range(3) if True or not x] == [0, 1, 2]

assert True or not x

# A wrong direction makes this loop leave without running the body; the
# counter bounds the iteration count so a flipped condition cannot hang.
entered = False
n = 0
while True or not x:
    entered = True
    n += 1
    if n > 3:
        break
assert entered, n
assert n == 4, n


def classify(v):
    match v:
        case int() if True or not x:
            return "matched"
        case _:
            return "fallback"


assert classify(5) == "matched"

# ============================================================
# 2. Trailing `not` over a non-bool short-circuit value: the VM must still
#    see a bool in front of the jump (previously an InvalidCastException).
# ============================================================

lst = [1]
flag = False

if lst or not flag:
    note("if-list")
assert last() == "if-list", last()

assert lst or not flag
assert (7 if (lst or not flag) else 8) == 7
assert [i for i in range(2) if lst or not flag] == [0, 1]

entered = False
n = 0
while lst or not flag:
    entered = True
    n += 1
    if n > 2:
        break
assert entered, n


def classify2(v):
    match v:
        case int() if lst or not flag:
            return "matched"
        case _:
            return "fallback"


assert classify2(5) == "matched"

# Falsy non-bool short-circuit value on the `and` side.
empty = []
if empty and not flag:
    raise AssertionError("empty list is falsy")
else:
    note("and-list")
assert last() == "and-list", last()

assert (empty and not flag) == []
assert (lst or not flag) == lst
assert ("" or not flag) is True
assert (0 or not flag) is True
assert (b"" and not flag) == b""


class Truthy:
    def __bool__(self):
        return True


class Falsy:
    def __bool__(self):
        return False


obj = Truthy()
if obj or not flag:
    note("or-truthy-obj")
assert last() == "or-truthy-obj", last()

fobj = Falsy()
if fobj and not flag:
    raise AssertionError("Falsy object is falsy")
else:
    note("and-falsy-obj")
assert last() == "and-falsy-obj", last()

assert (obj or not flag) is obj
assert (fobj and not flag) is fobj

# ============================================================
# 3. `not` in other positions and nestings stays correct
# ============================================================

y = True
assert (not x and y) is True
assert (y and not x) is True
assert (not x or y) is True
assert (not not x) is False
assert (not (x == 0)) is False
assert (not x and y or not y) is True
assert (not 1 in [1]) is False

a = 0
b = 0
assert (not (a or b)) is True
if not (a or b):
    note("not-boolop")
assert last() == "not-boolop", last()

# `not X` as the whole test (the case the removed fusion was written for).
if not flag:
    note("not-name")
assert last() == "not-name", last()

assert not flag
assert [i for i in range(2) if not flag] == [0, 1]

entered = False
while not flag:
    entered = True
    break
assert entered

assert (1 if not flag else 2) == 1

# ============================================================
# 4. Async comprehension `if` clause
# ============================================================


class AsyncRange:
    def __init__(self, n):
        self.n = n
        self.i = 0

    def __aiter__(self):
        return self

    async def __anext__(self):
        if self.i >= self.n:
            raise StopAsyncIteration
        val = self.i
        self.i += 1
        return val


def run_async():
    results = []

    async def inner():
        results.append([i async for i in AsyncRange(3) if True or not x])
        results.append([i async for i in AsyncRange(3) if lst or not flag])
        results.append([i async for i in AsyncRange(3) if not flag])
        return results

    coro = inner()
    try:
        coro.send(None)
    except StopIteration:
        pass
    return results


assert run_async() == [[0, 1, 2], [0, 1, 2], [0, 1, 2]]
