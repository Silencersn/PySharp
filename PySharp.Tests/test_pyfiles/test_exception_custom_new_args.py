"""
an exception subclass overriding __new__ but not __init__
still records the original instantiation arguments in e.args, because
CPython's type_call calls the inherited BaseException.__init__ with the
original arguments (which re-bind the args tuple). The old PySharp
BaseException.__init__ was a no-op, so a custom __new__ that consumed
the arguments left e.args empty.

CPython 3.14 reference: BaseException_init rejects keywords
("<Cls>() takes no keyword arguments") and re-binds self->args.

:kind: test
"""

class E(Exception):
    def __new__(cls, *a, **k):
        return super().__new__(cls)

e = E(9)
assert e.args == (9,)
assert str(e) == "9"
assert repr(e) == "E(9)"

# keyword rejection through both paths, named after the actual class
class K(Exception):
    def __new__(cls, *a, **k):
        return super().__new__(cls)

def err(fn):
    try:
        fn()
        return "no-error"
    except TypeError as ex:
        return str(ex)

assert err(lambda: K(9, x=1)) == "K() takes no keyword arguments"
assert err(lambda: Exception(a=1)) == "Exception() takes no keyword arguments"
assert err(lambda: Exception(1, a=2)) == "Exception() takes no keyword arguments"
assert err(lambda: BaseException(x=1)) == "BaseException() takes no keyword arguments"

# the dunders re-bind manually
e2 = BaseException.__new__(Exception)
assert e2.args == ()
BaseException.__init__(e2, 9)
assert e2.args == (9,)

# a custom __init__ still receives the original arguments
class I(Exception):
    def __init__(self, x):
        self.x = x

assert I(5).x == 5
assert I(5).args == (5,)

# plain subclasses and built-in exceptions unchanged
class P(Exception):
    pass

p = P(1, 2, 3)
assert p.args == (1, 2, 3) and str(p) == "(1, 2, 3)"
v = ValueError('bad')
assert v.args == ('bad',) and str(v) == 'bad' and repr(v) == "ValueError('bad')"

# a custom __new__ forwarding args through super: the inherited __init__
# re-binds e.args to the ORIGINAL call arguments, overwriting what
# __new__ stored (CPython type_call always calls __init__ with them)
class S(Exception):
    def __new__(cls, x):
        return super().__new__(cls, x, 'extra')

assert S(1).args == (1,)

# the raise machinery is unaffected
try:
    raise E('boom')
except E as ex:
    assert ex.args == ('boom',) and str(ex) == 'boom'

print("test_exception_custom_new_args passed")
