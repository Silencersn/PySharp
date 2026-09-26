"""
using NotImplemented in a boolean context must raise
TypeError('NotImplemented should not be used in a boolean context') like
CPython 3.12+ (hard error since 3.14; PySharp previously warned and
returned True). NotImplemented as a plain value keeps working — short
circuit operators, direct dunder calls, and containers that never enter a
boolean context are unaffected.

CPython 3.14 reference:
    bool(NotImplemented)      -> TypeError
    'x' if NotImplemented     -> TypeError
    not NotImplemented        -> TypeError
    all([NotImplemented])     -> TypeError
    0 or NotImplemented       -> NotImplemented (no bool evaluation)
    (1).__eq__(NotImplemented)-> NotImplemented (plain value is fine)

:kind: test
"""

try:
    bool(NotImplemented)
except TypeError as e:
    assert str(e) == 'NotImplemented should not be used in a boolean context'
else:
    raise AssertionError('bool(NotImplemented) must raise TypeError')

try:
    'taken' if NotImplemented else 'else'
except TypeError as e:
    assert str(e) == 'NotImplemented should not be used in a boolean context'
else:
    raise AssertionError('if NotImplemented must raise TypeError')

try:
    not NotImplemented
except TypeError:
    pass
else:
    raise AssertionError('not NotImplemented must raise TypeError')

try:
    all([NotImplemented])
except TypeError:
    pass
else:
    raise AssertionError('all([NotImplemented]) must raise TypeError')

# boolean evaluation is only reached when actually needed
assert (0 or NotImplemented) is NotImplemented

# as a plain value NotImplemented stays legal
assert repr(NotImplemented) == 'NotImplemented'
assert type(NotImplemented).__name__ == 'NotImplementedType'
assert (1).__eq__(NotImplemented) is NotImplemented
assert list(filter(lambda v: True, [NotImplemented])) == [NotImplemented]
assert [NotImplemented].pop() is NotImplemented

print("test_notimplemented_bool passed")
