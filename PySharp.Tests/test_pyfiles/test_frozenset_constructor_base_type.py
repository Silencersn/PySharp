"""
frozenset() of an exact frozenset returns the instance itself
(CPython make_new_set fast path), but a frozenset SUBCLASS instance must be
copied into the base type — the constructor used to pass subclass instances
through unchanged, and constructing a subclass from an exact frozenset even
retyped the original object in place. set() and bytes() keep their copying
semantics as controls.

CPython 3.14.6 reference:
    type(frozenset(FS([1]) )).__name__    -> 'frozenset'
    sorted(frozenset(FS([1])))            -> [1]
    frozenset(f) is f                     -> True
    type(FS(f) ).__name__                 -> 'FS'
    FS(f) is f                            -> False
    type(f).__name__ after FS(f)          -> 'frozenset'
    type(FS(FS([1]) )).__name__           -> 'FS'
    frozenset.__new__(frozenset, f) is f  -> True
    type(frozenset.__new__(FS, [1]) )     -> FS
    type(set(FS([1]) )).__name__          -> 'set'
    set(f) is f                           -> False
    type(bytes(bytes(b'xy') )).__name__   -> 'bytes'

:kind: test
"""

class FS(frozenset):
    pass


class S(set):
    pass


f = frozenset([1])

# a subclass instance is copied into the base type
assert type(frozenset(FS([1]) )).__name__ == 'frozenset'
assert sorted(frozenset(FS([1]))) == [1]
assert type(frozenset(S([1]) )).__name__ == 'frozenset'

# exact frozenset keeps the identity fast path
assert frozenset(f) is f

# constructing a subclass copies — never passes the original through
sub = FS(f)
assert type(sub).__name__ == 'FS'
assert sub is not f
assert sorted(sub) == [1]
assert type(f).__name__ == 'frozenset'

# subclass from subclass is a fresh instance too
inner = FS([2])
outer = FS(inner)
assert type(outer).__name__ == 'FS'
assert outer is not inner
assert sorted(outer) == [2]

# dunder __new__ follows the same rules
assert frozenset.__new__(frozenset, f) is f
assert type(frozenset.__new__(FS, [1]) ).__name__ == 'FS'

# mutable set() always copies (base type for subclass args)
assert type(set(FS([1]) )).__name__ == 'set'
assert set(f) is not f

# immutable bytes() copies subclass instances into the base type
assert type(bytes(bytes(b'xy') )).__name__ == 'bytes'

# plain forms unchanged
assert type(frozenset()).__name__ == 'frozenset'
assert type(frozenset({1}) ).__name__ == 'frozenset'
assert bool(frozenset()) is False
assert (type(FS()).__name__, sorted(FS())) == ('FS', [])

print("test_frozenset_constructor_base_type passed")
