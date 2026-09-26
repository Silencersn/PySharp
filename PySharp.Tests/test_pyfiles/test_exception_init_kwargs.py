"""
exception subclass __init__ receives keyword arguments.

BaseException.__new__ (and BaseExceptionGroup.__new__) accept and ignore
keyword arguments like CPython — the kwargs rejection lives in __init__ —
so type_call dispatches the original instantiation kwargs to a subclass's
custom __init__, and raise takes the same path. Subclasses without a
custom __init__ still reject kwargs from __init__ with the runtime type
name, and BaseExceptionGroup validates its args before kwargs can matter.

CPython 3.14 reference (Objects/exceptions.c BaseException_new/init,
BaseExceptionGroup_new).

:kind: test
"""

# kwargs rejected from __init__ when no custom __init__ intervenes
try:
    Exception(code=7)
except TypeError as e:
    assert str(e) == "Exception() takes no keyword arguments"
else:
    assert False, "TypeError expected"

class E2(Exception):
    pass
try:
    E2(code=7)
except TypeError as e:
    assert str(e) == "E2() takes no keyword arguments"
else:
    assert False, "TypeError expected"

# the issue repro: kwonly __init__ + super
class E3(Exception):
    def __init__(self, *, code):
        self.code = code
        super().__init__(code)
e3 = E3(code=7)
assert e3.code == 7
assert e3.args == (7,)

# custom __init__ that never calls super: args stays ()
class E4(Exception):
    def __init__(self, *, code):
        self.code = code
e4 = E4(code=9)
assert e4.code == 9
assert e4.args == ()

# custom __new__ only: __init__ still rejects kwargs
class E5(Exception):
    def __new__(cls, *args, **kw):
        return super().__new__(cls)
try:
    E5(code=7)
except TypeError as e:
    assert str(e) == "E5() takes no keyword arguments"
else:
    assert False, "TypeError expected"

# mixed positional + kwonly
class E6(Exception):
    def __init__(self, a, *, code):
        self.code = code
        super().__init__(a, code)
assert E6(1, code=2).args == (1, 2)

# raise takes the same instantiation path
class E7(Exception):
    def __init__(self, *, code):
        super().__init__(code)
try:
    raise E7(code=8)
except E7 as e:
    assert e.args == (8,)

# varargs + kwonly default
class E8(Exception):
    def __init__(self, *args, code=1):
        super().__init__(*args, code)
assert E8(5).args == (5, 1)

# exception group subclasses forward kwargs the same way
class G(ExceptionGroup):
    def __init__(self, msg, excs, *, code):
        self.code = code
        super().__init__(msg, excs)
g = G("m", [ValueError("x")], code=7)
assert g.code == 7
assert g.message == "m"

# group args are validated before kwargs could matter
try:
    ExceptionGroup("m", [], code=7)
except ValueError as e:
    assert str(e) == "second argument (exceptions) must be a non-empty sequence"
else:
    assert False, "ValueError expected"

# group without custom __init__ still rejects kwargs
try:
    ExceptionGroup("m", [ValueError("x")], code=7)
except TypeError as e:
    assert str(e) == "ExceptionGroup() takes no keyword arguments"
else:
    assert False, "TypeError expected"

# a custom __new__ does not bypass __init__'s kwargs rejection
class G2(ExceptionGroup):
    def __new__(cls, *args, **kw):
        return super().__new__(cls, *args)
try:
    G2("m", [], code=7)
except ValueError as e:
    assert str(e) == "second argument (exceptions) must be a non-empty sequence"
else:
    assert False, "ValueError expected"

print("test_exception_init_kwargs passed")
