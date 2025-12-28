class MyIter:
    def __iter__(self):
        self.range = 3
        return self

    def __next__(self):
        if self.range > 0:
            self.range -= 1
            return self.range
        raise StopIteration('MyIter Stop')

    def __getattr__(self, item):
        if item == 'send':
            def send(value):
                return next(self)
            return send

        if item == 'close':
            def close():
                return 99999
            return close
        raise AttributeError

def subgen():
    val = (yield 11)
    assert val == 1
    val = (yield 22)
    assert val == 2

def gen():
    try:
        a = (yield from subgen())
        assert a is None
        a = (yield from MyIter())
        assert a == 'MyIter Stop'
        yield from MyIter()
    except TypeError:
        assert False, 'TypeError should not be raised'

g = gen()
assert hasattr(g, 'send')
assert hasattr(g, 'close')

# lambda generator checks
f = (lambda x: (yield x))
g2 = f(1)
assert next(g2) == 1

# send None
ret = g.send(None)
assert ret == 11

# send 1
ret = g.send(1)
assert ret == 22

# send 2
ret = g.send(2)
assert ret == 2

# send 3
ret = g.send(3)
assert ret == 1

# send 4
ret = g.send(4)
assert ret == 0

# send 5
ret = g.send(5)
assert ret == 2

# send 6
ret = g.send(6)
assert ret == 1

# close
g.close()

# send 7, should raise StopIteration
try:
    g.send(7)
    assert False, 'Should have raised StopIteration'
except StopIteration:
    pass

# over