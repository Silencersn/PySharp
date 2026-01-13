x = 0
def outer():
    y = 1
    z = 0
    def inner():
        nonlocal y, z
        global x
        y += 1
        z += 2
        x += 3
        return y, z, x
    return inner

f = outer()
assert f() == (2, 2, 3)
assert f() == (3, 4, 6)

lst = []
def make_funcs():
    acc = 0
    for i in range(3):
        def f(j=i):
            nonlocal acc
            acc += j
            return acc
        lst.append(f)
make_funcs()
assert lst[0]() == 0
assert lst[1]() == 1
assert lst[2]() == 3

result = []
def test_loop():
    for i in range(5):
        if i == 2:
            continue
        if i == 4:
            break
        def closure(val=i):
            return val
        result.append(closure)
test_loop()
assert [f() for f in result] == [0, 1, 3]

global_var = 10
def gfunc():
    global global_var
    global_var += 5
    return global_var
assert gfunc() == 15
assert global_var == 15



def test2(a=1, c=2):
    a = 'a'
    b = 'b'
    c = 'c'
    def inner():
        print(c)
        def inner2():
            return a
        return inner2
    def inner3():
        return b
    return inner, inner3

inner, inner3 = test2()
inner2 = inner()
assert inner2() == 'a'
assert inner3() == 'b'

def test(value):
    class InnerA:
        print('value:', value)
        def test1(self):
            __class__ = int
            return lambda self: (super().__repr__(), value)

        def test2(self):
            __class__
            pass
    return InnerA



A = test(1234)
t1 = A().test1
t2 = A().test2
assert t1()(123)



def wrapper():
    value = 1
    def inner1():
        return value + 1
    def inner2():
        return value + 2
    return inner1, inner2

i1, i2 = wrapper()
assert i1() == 2
assert i2() == 3
