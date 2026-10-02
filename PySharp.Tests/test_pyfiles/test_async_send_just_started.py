"""gen.send, coroutine.send and asyncgen.asend reject a non-None value before the first yield with three distinct just-started messages.

:kind: test
"""

# Regression: the just-started send rejection names the kind of the
# receiver — CPython gen_send_ex2 picks the message from the generator's
# type, so an async generator must not fall back to the plain
# generator wording.


def gen():
    yield 1


g = gen()
try:
    g.send("v")
except TypeError as e:
    print("gen:", type(e).__name__ + ":", e)


async def coro():
    pass


c = coro()
try:
    c.send("v")
except TypeError as e:
    print("coro:", type(e).__name__ + ":", e)


async def agen():
    yield 1


a = agen()
as_ = a.asend("v")
try:
    as_.send(None)
except TypeError as e:
    print("agen:", type(e).__name__ + ":", e)
