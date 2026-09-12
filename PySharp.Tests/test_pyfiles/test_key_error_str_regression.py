"""
Regression: str(KeyError) must apply repr() to a single argument
(CPython KeyError_str), keeping quotes and type information. Every other
arity keeps BaseException's form: no args is the empty string and several
args format the args tuple. Subclasses inherit the behavior unless they
define their own __str__.
"""

# single argument goes through repr
assert str(KeyError('a')) == "'a'"
assert str(KeyError(1)) == '1'
assert str(KeyError(1.5)) == '1.5'
assert str(KeyError(None)) == 'None'
assert str(KeyError(('x', 1))) == "('x', 1)"
assert str(KeyError([1, 'x'])) == "[1, 'x']"

# repr dispatches to the argument's own __repr__
class R:
    def __repr__(self):
        return 'R!'

assert str(KeyError(R())) == 'R!'

# no args and multiple args keep the base form
assert str(KeyError()) == ''
assert str(KeyError('a', 'b')) == "('a', 'b')"

# repr of the exception itself is unchanged
assert repr(KeyError('a')) == "KeyError('a')"
assert repr(KeyError()) == 'KeyError()'
assert repr(KeyError('a', 'b')) == "KeyError('a', 'b')"

# f-string and print paths share the slot
e = KeyError('k')
assert f"{e}" == "'k'"

# subclasses inherit the behavior; an explicit __str__ still wins
class S(KeyError):
    pass

assert str(S('q')) == "'q'"

class S2(KeyError):
    def __str__(self):
        return 'custom'

assert str(S2('q')) == 'custom'

# a str subclass argument uses its inherited repr
class MyStr(str):
    pass

assert str(KeyError(MyStr('m'))) == "'m'"

print("test_key_error_str_regression passed")
