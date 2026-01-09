# Basic assignment expression
assert (x := 10) == 10
assert x == 10

# Assignment in list comprehension
lst = [y := i * 2 for i in range(3)]
assert lst == [0, 2, 4]
assert y == 4

# Assignment in while loop
i = 0
result = []
while (val := i) < 3:
    result.append(val)
    i += 1
assert result == [0, 1, 2]
assert val == 3

# Assignment in if statement
if (n := len("hello")) > 3:
    assert n == 5

# Assignment in function call argument
def f(a): return a + 1
assert f(b := 7) == 8
assert b == 7

# Assignment in tuple
assert ((a := 1), (b := 2)) == (1, 2)
assert a == 1 and b == 2

# Assignment in dictionary comprehension
d = {k: (v := k * 2) for k in range(2)}
assert v == 2

# Assignment in nested expression
assert ((z := (y := 3) + 2) == 5)
assert y == 3 and z == 5
