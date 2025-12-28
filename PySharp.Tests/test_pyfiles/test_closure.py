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
