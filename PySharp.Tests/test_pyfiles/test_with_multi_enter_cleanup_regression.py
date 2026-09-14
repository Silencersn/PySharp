# Regression: a with-item's handler region must restore the stack to before
# the __enter__ result (CPython SETUP_WITH semantics). The region recorded
# its depth with the value still pushed, so any exception that unwound with
# extra items above the [exit, manager] pairs — a later manager's __enter__
# failing, operand temporaries mid-expression in the body, an as-target
# unpack failure — left strays that made the handler call the manager object
# itself (TypeError: 'CM' object is not callable), losing the original
# exception and skipping the already-entered managers' __exit__.

LOG = []

class CM:
    def __init__(self, tag, enter_raise=False, suppress=False):
        self.tag = tag
        self.enter_raise = enter_raise
        self.suppress = suppress
    def __enter__(self):
        LOG.append(f"enter:{self.tag}")
        if self.enter_raise:
            raise RuntimeError(f"enter-{self.tag}")
        return self.tag
    def __exit__(self, et, ev, tb):
        LOG.append(f"exit:{self.tag}:{et.__name__ if et else None}")
        return self.suppress

# original repro: second manager's __enter__ raises
try:
    with CM("a"), CM("b", enter_raise=True):
        pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}")
assert LOG == ['enter:a', 'enter:b', 'exit:a:RuntimeError', 'caught:enter-b']

# third manager fails: exits run in reverse order
LOG.clear()
try:
    with CM("c"), CM("d"), CM("e", enter_raise=True):
        pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}")
assert LOG == ['enter:c', 'enter:d', 'enter:e', 'exit:d:RuntimeError',
               'exit:c:RuntimeError', 'caught:enter-e']

# middle of three fails: the third never enters
LOG.clear()
try:
    with CM("p1"), CM("p2", enter_raise=True), CM("p3"):
        pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}")
assert LOG == ['enter:p1', 'enter:p2', 'exit:p1:RuntimeError', 'caught:enter-p2']

# first manager fails: nothing to clean up
LOG.clear()
try:
    with CM("first", enter_raise=True), CM("second"):
        pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}")
assert LOG == ['enter:first', 'caught:enter-first']

# parenthesized form triggers the same path
LOG.clear()
try:
    with (CM("x"), CM("y", enter_raise=True)):
        pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}")
assert LOG == ['enter:x', 'enter:y', 'exit:x:RuntimeError', 'caught:enter-y']

# the already-entered manager may suppress the enter failure
LOG.clear()
try:
    with CM("w", suppress=True), CM("z", enter_raise=True):
        pass
    LOG.append("suppressed")
except RuntimeError:
    LOG.append("not-suppressed")
assert LOG == ['enter:w', 'enter:z', 'exit:w:RuntimeError', 'suppressed']

# body exception with operand temporaries pending above the pairs
LOG.clear()
def ident(v):
    return v
try:
    with CM("t"):
        ident(1 / 0)
except ZeroDivisionError:
    LOG.append("caught-zde")
assert LOG == ['enter:t', 'exit:t:ZeroDivisionError', 'caught-zde']

# as-targets: the first binding stays usable in the except
LOG.clear()
try:
    with CM("q1") as v1, CM("q2", enter_raise=True) as v2:
        pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}:{v1}")
assert LOG == ['enter:q1', 'enter:q2', 'exit:q1:RuntimeError', 'caught:enter-q2:q1']

# as-target unpack failure: the manager's __exit__ still runs (CPython
# keeps the store inside the protected region)
class OneElem(CM):
    def __enter__(self):
        LOG.append(f"enter:{self.tag}")
        return (1,)
LOG.clear()
try:
    with OneElem("u") as (a, b):
        pass
except ValueError:
    LOG.append("unpack-caught")
assert LOG == ['enter:u', 'exit:u:ValueError', 'unpack-caught']

# return through the body: exits run in reverse with no exception
LOG.clear()
def f_ret():
    with CM("r1"), CM("r2"):
        return 42
assert f_ret() == 42
assert LOG == ['enter:r1', 'enter:r2', 'exit:r2:None', 'exit:r1:None']

# nested with statements
LOG.clear()
try:
    with CM("n1"):
        with CM("n2"), CM("n3", enter_raise=True):
            pass
except RuntimeError as ex:
    LOG.append(f"caught:{ex}")
assert LOG == ['enter:n1', 'enter:n2', 'enter:n3', 'exit:n2:RuntimeError',
               'exit:n1:RuntimeError', 'caught:enter-n3']

# generator suspends inside the with body, resumes, exits run once
LOG.clear()
def gen():
    with CM("g1"), CM("g2"):
        yield 1
        yield 2
    yield 'end'
assert list(gen()) == [1, 2, 'end']
assert LOG == ['enter:g1', 'enter:g2', 'exit:g2:None', 'exit:g1:None']

# async with: the second __aenter__ failing cleans the first manager
LOG.clear()
class ACM:
    def __init__(self, tag, enter_raise=False):
        self.tag = tag
        self.enter_raise = enter_raise
    async def __aenter__(self):
        LOG.append(f"enter:{self.tag}")
        if self.enter_raise:
            raise RuntimeError(f"enter-{self.tag}")
        return self.tag
    async def __aexit__(self, et, ev, tb):
        LOG.append(f"exit:{self.tag}:{et.__name__ if et else None}")
        return False

async def af():
    async with ACM("a1"), ACM("a2", enter_raise=True):
        pass

coro = af()
while True:
    try:
        coro.send(None)
    except StopIteration:
        break
    except RuntimeError as ex:
        LOG.append(f"caught:{ex}")
        break
assert LOG == ['enter:a1', 'enter:a2', 'exit:a1:RuntimeError', 'caught:enter-a2']
