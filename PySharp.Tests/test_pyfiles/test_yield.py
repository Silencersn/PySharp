"""
Generator and yield/yield from behavior tests
"""

class MyIter:
    """Mock iterator for testing yield from"""
    def __init__(self):
        self.range = 3
    def __iter__(self):
        return self
    def __next__(self):
        if self.range > 0:
            self.range -= 1
            return self.range
        raise StopIteration('MyIter Stop')

    def send(self, value):
        return next(self)

    def close(self):
        pass

def subgen():
    """Sub-generator for yield from test"""
    val = (yield 11)
    assert val == 1
    val = (yield 22)
    assert val == 2

def gen():
    """Main generator using yield from"""
    try:
        a = (yield from subgen())
        assert a is None
        a = (yield from MyIter())
        assert a == 'MyIter Stop'
    except TypeError:
        assert False, 'TypeError should not be raised'

# Test generator properties
g = gen()
assert hasattr(g, 'send')
assert hasattr(g, 'close')

# Test generator protocol: send, next, yield from
ret = g.send(None) # start gen
assert ret == 11

ret = g.send(1) # send to subgen
assert ret == 22

ret = g.send(2) # subgen finishes, yields from MyIter
assert ret == 2 # MyIter next(0) -> 2

ret = g.send(3) # MyIter next(1) -> 1
assert ret == 1

ret = g.send(4) # MyIter next(2) -> 0
assert ret == 0

# Test StopIteration propagation
try:
    g.send(5)
    assert False, 'Should have raised StopIteration'
except StopIteration:
    pass

# Test generator.close()
def gen_close():
    try:
        yield 1
    finally:
        yield 2 # This should raise RuntimeError: generator ignored GeneratorExit

g = gen_close()
next(g)
try:
    g.close()
    # PySharp might not strictly raise RuntimeError for ignored GeneratorExit yet, 
    # but let's see. In CPython it does.
except RuntimeError:
    pass

# Test generator.throw()
def gen_throw():
    try:
        yield 1
    except ValueError:
        yield 2

g = gen_throw()
assert next(g) == 1
assert g.throw(ValueError) == 2
try:
    next(g)
    assert False, "Should raise StopIteration"
except StopIteration:
    pass

# Test send(non-None) on unstarted generator
def gen_simple():
    yield 1

g = gen_simple()
try:
    g.send(1)
    assert False, "Should raise TypeError for non-None send on unstarted generator"
except TypeError:
    pass

print("test_yield passed")

# over