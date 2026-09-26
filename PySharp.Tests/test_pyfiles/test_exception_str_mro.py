"""Exception __str__ is resolved through the first MRO entry that defines __str__ in its own dict, so a native base inheriting the default (TypeError) must not mask a later base's real method (KeyError's repr-style __str__).

:kind: test
"""
# Exception __str__ MRO, aligned with CPython:
#
# fixup_slot_dispatchers resolves the defaultable slots through the first
# MRO entry defining the dunder in its own dict — a native base that
# merely inherits the slot pointer (TypeError for __str__) must not mask
# a later native base's real method (KeyError's repr-style __str__).

# single native base keeps KeyError's repr-style str
class KE(KeyError):
    pass
assert str(KE('k')) == "'k'"

# KeyError behind a native base without its own __str__: KeyError's wins
class Dual(TypeError, KeyError):
    pass
assert str(Dual('d')) == "'d'"
assert str(Dual()) == ''
assert str(Dual(1, 2)) == '(1, 2)'

# native base order flipped: same result
class Dual2(KeyError, TypeError):
    pass
assert str(Dual2('d2')) == "'d2'"

# OSError defines its own __str__: first base wins as before
class Dual3(OSError, KeyError):
    pass
assert str(Dual3('d3')) == 'd3'

# three bases: the walk crosses AttributeError to KeyError
class Dual4(AttributeError, KeyError):
    pass
assert str(Dual4('d4')) == "'d4'"

# repr keeps BaseException's form for every shape
assert repr(Dual('r')) == "Dual('r')"

# single-user-subclass of KeyError keeps the repr-style str
class MyErr(KeyError):
    pass
assert str(MyErr('m')) == "'m'"

# a user __str__ still beats every native base
class Over(TypeError, KeyError):
    def __str__(self):
        return 'custom'
assert str(Over('o')) == 'custom'

# identity hash/eq semantics for exception instances are untouched
d = {}
key = Dual('x')
d[key] = 1
assert d[key] == 1 and key in d and Dual('y') not in d

# two default __str__ bases keep the default form
class Dual5(ValueError, TypeError):
    pass
assert str(Dual5('d5')) == 'd5'

print("test_exception_str_mro passed")
