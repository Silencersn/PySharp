"""
Regression: vars(obj) must raise TypeError for objects without a __dict__
(CPython), instead of returning {}; obj.__dict__ must raise AttributeError.

CPython 3.14 reference:
    vars(42) / vars('abc') / vars([1, 2]) / vars(len) / vars(code)  -> TypeError
    (42).__dict__                                                    -> AttributeError
    vars(o) for a plain user instance                                -> the instance dict
"""

import math

# --- Objects WITHOUT __dict__: vars() must raise TypeError ---
for o in (42, 1.5, 'abc', [1, 2], {1: 2}, range(3), object()):
    try:
        vars(o)
        assert False, f'vars({o!r}) should raise TypeError'
    except TypeError:
        pass

try:
    vars(len)
    assert False, 'vars(len) should raise TypeError'
except TypeError:
    pass

try:
    vars([].append)
    assert False, 'vars(method) should raise TypeError'
except TypeError:
    pass

try:
    vars(compile('x = 1', 'f', 'exec'))
    assert False, 'vars(code) should raise TypeError'
except TypeError:
    pass

# --- obj.__dict__ on objects without __dict__: AttributeError ---
try:
    (42).__dict__
    assert False, '(42).__dict__ should raise AttributeError'
except AttributeError:
    pass

# --- Objects WITH __dict__: vars() returns the instance dict ---
class Obj:
    pass

o = Obj()
o.a = 1
assert vars(o)['a'] == 1
assert o.__dict__['a'] == 1

def user_func():
    pass
assert isinstance(vars(user_func), dict)
assert isinstance(vars(Obj), dict)
assert isinstance(vars(math), dict)

# Exception instances have a real __dict__ (CPython allows e.note = 1).
e = ValueError('boom')
e.note = 1
assert vars(e)['note'] == 1

print("test_vars_dict_regression passed")
