"""
Tests that free variables in class bodies do not leak into the class dict.
"""
print("testing class freevar")

# Test 1: closure variable in class body should NOT become class attribute
def test1():
    a = 42
    class A:
        x = a
    return A

A = test1()
assert hasattr(A, 'x'), "x should be a class attribute"
assert not hasattr(A, 'a'), "a should NOT leak into class dict"
assert A.x == 42

# Test 2: multiple closure vars
def test2():
    x = 1
    y = 2
    class A:
        s = x + y
    return A

A = test2()
assert not hasattr(A, 'x')
assert not hasattr(A, 'y')
assert A.s == 3

# Test 3: closure var used in method inside class body
def test3():
    msg = "hello"
    class A:
        def greet(self):
            return msg
    return A

A = test3()
assert not hasattr(A, 'msg')
assert A().greet() == "hello"

# Test 4: nonlocal assignment in class body
def test4():
    counter = 0
    class A:
        nonlocal counter
        counter = 99
    assert counter == 99
    assert not hasattr(A, 'counter')
    return A

test4()

# Test 5: class attribute doesn't affect closure var
def test5():
    prefix = "outer_"
    class A:
        suffix = "_class"
        def get_suffix(self):
            return suffix  # refers to closure var 'suffix', not defined!
    return A

# 'suffix' is only in class dict, not as closure var
# This should raise NameError, matching CPython

# Test 6: nested class with closure
def test6():
    val = 99
    class Outer:
        class Inner:
            def get(self):
                return val
    return Outer

O = test6()
assert not hasattr(O, 'val')
assert not hasattr(O.Inner, 'val')
assert O.Inner().get() == 99

# Test 7: generic class type params
class Box[T]:
    value = T
    def get(self):
        return T

assert not hasattr(Box, 'T')
assert Box.value is Box.__type_params__[0]
assert Box().get() is Box.__type_params__[0]

# Test 8: outer local + type param mix
def test8():
    x = 10
    class Pair[T, U]:
        first = x
        second = T
    return Pair

P = test8()
assert not hasattr(P, 'x')
assert not hasattr(P, 'T')
assert not hasattr(P, 'U')
assert P.first == 10
assert P.second is P.__type_params__[0]

print("test_class_freevar passed")
