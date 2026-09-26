"""
__init__ must return None when called through instantiation,
like CPython's slot_tp_init (Objects/typeobject.c): any other return
value raises TypeError: __init__() should return None, not '<type>'.
PySharp used to silently accept the value and finish construction.

The check applies to the type-call path only - a direct __init__() call
is a plain method call and must stay exempt - and to class creation
through a metaclass whose __init__ returns a value.

:kind: test
"""


def expect_init_type_error(fn, type_name):
    try:
        fn()
    except TypeError as e:
        assert f"__init__() should return None, not '{type_name}'" in str(e), str(e)
    else:
        assert False, "should raise TypeError"


class RetInt:
    def __init__(self):
        return 42


class RetStr:
    def __init__(self):
        return "hello"


class RetSelf:
    def __init__(self):
        return self


class RetGen:
    def __init__(self):
        yield 1


# red cases: non-None returns are rejected with the value's type name
expect_init_type_error(RetInt, "int")
expect_init_type_error(RetStr, "str")
expect_init_type_error(RetSelf, "RetSelf")
expect_init_type_error(RetGen, "generator")


# red case: the metaclass path is checked too
class Meta(type):
    def __init__(cls, name, bases, ns):
        return 42


try:
    Meta("M", (), {})
except TypeError as e:
    assert "__init__() should return None, not 'int'" in str(e), str(e)
else:
    assert False, "metaclass __init__ return must be checked"


# red case: returning None explicitly is fine (and the instance works)
class GoodInit:
    def __init__(self):
        self.x = 1
        return None


g = GoodInit()
assert g.x == 1


# guard: a direct __init__() call is exempt (CPython checks only the
# instantiation path)
b = object.__new__(RetInt)
assert b.__init__() == 42


# guards: inherited object.__init__ and __new__-skip stay aligned
class Plain:
    pass


assert isinstance(Plain(), Plain)


class SkipInit:
    def __new__(cls):
        return object.__new__(GoodInit)

    def __init__(self):
        return 99


s = SkipInit()
assert isinstance(s, GoodInit)
assert not hasattr(s, "x")   # GoodInit's __init__ never ran

print("test_init_return_check passed")
