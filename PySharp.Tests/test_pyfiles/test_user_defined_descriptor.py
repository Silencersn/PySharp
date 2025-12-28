class Desc:
    def __init__(self, name):
        self.name = name
    def __get__(self, instance, owner):
        return self.name, instance, owner

class A:
    test_prop = Desc('From A')
    aaa = 123

class B(A):
    test_prop = Desc('From B')
    def __repr__(self):
        return super(B, self).__repr__()

b = B()
name, instance, owner = b.test_prop
assert name == 'From B'
assert isinstance(instance, B)
assert owner is B

t = type(list.append)
assert hasattr(t, '__get__')