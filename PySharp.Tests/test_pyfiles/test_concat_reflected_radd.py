"""
str/bytes/tuple `+` must give the right operand's
__radd__ the first chance, like CPython (whose str/bytes/tuple have no
nb_add — the reflected slot runs before the sq_concat fallback). The
old slots raised the concat TypeError immediately, so __radd__ never
ran, and libraries relying on `'a' + custom` were broken.

CPython 3.14 reference: concat TypeError only when no reflected method
exists or it declines (returns NotImplemented); the message is
unchanged ("can only concatenate str (not \"X\") to str", etc.).

:kind: test
"""

class R:
    def __radd__(self, o):
        return ('radd', type(o).__name__)

class N:
    pass

r, n = R(), N()

def err(fn):
    try:
        return ('ok', fn())
    except Exception as e:
        return (type(e).__name__, str(e))

# the reflected __radd__ runs for str, bytes and tuple
assert 'a' + r == ('radd', 'str')
assert b'a' + r == ('radd', 'bytes')
assert (1,) + r == ('radd', 'tuple')
# int and list already dispatched correctly
assert 1 + r == ('radd', 'int')
assert [1] + r == ('radd', 'list')

# without __radd__ the concat TypeError messages are unchanged
assert err(lambda: 'a' + n) == ('TypeError', 'can only concatenate str (not "N") to str')
assert err(lambda: (1,) + n) == ('TypeError', 'can only concatenate tuple (not "N") to tuple')
assert err(lambda: b'a' + n) == ('TypeError', "can't concat N to bytes")
assert err(lambda: 'a' + 1) == ('TypeError', 'can only concatenate str (not "int") to str')

# normal concat faces unchanged
assert err(lambda: 'a' + 'b') == ('ok', 'ab')
assert err(lambda: (1,) + (2,)) == ('ok', (1, 2))
assert err(lambda: b'a' + b'b') == ('ok', b'ab')
assert err(lambda: 'a' + bytearray(b'c')) == ('TypeError', 'can only concatenate str (not "bytearray") to str')

# a reflected method that declines falls through to the concat error
class Declining:
    def __radd__(self, o):
        return NotImplemented

assert err(lambda: 'a' + Declining()) == ('TypeError', 'can only concatenate str (not "Declining") to str')

# an inherited __radd__ is found through the subclass
class R2(R):
    pass

assert 'a' + R2() == ('radd', 'str')

# augmented concatenation follows the same rules
s = 'a'
try:
    s += n
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == 'can only concatenate str (not "N") to str'

t = (1,)
t += r
assert t == ('radd', 'tuple')

# the dunder API stays strict
assert err(lambda: 'a'.__add__(1)) == ('TypeError', 'can only concatenate str (not "int") to str')

print("test_concat_reflected_radd passed")
