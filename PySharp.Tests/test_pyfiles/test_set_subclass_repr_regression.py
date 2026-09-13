"""
Regression: set and frozenset subclass instances must render with the
subclass type name — TypeName({…}) when non-empty and TypeName() when empty
(CPython set_repr / frozenset_repr) — so they stay distinguishable from the
builtin {…} form. dict / list / tuple subclasses have no such rule and keep
rendering bare.

CPython 3.14 reference:
    class Y(set): pass
    repr(Y([1, 2]))  -> 'Y({1, 2})'
    repr(E())        -> 'E()'
    str(Y([1]))      -> 'Y({1})'
    class F(frozenset)
    repr(F([1]))     -> 'F({1})'
    repr(F())        -> 'F()'
    repr(X()) dict subclass    -> '{}'
    repr(L([1])) list subclass -> '[1]'
"""

class Y(set):
    pass


class E(set):
    pass


class F(frozenset):
    pass


class Z(Y):
    pass


assert repr(Y([1, 2])) == 'Y({1, 2})'
assert repr(Y([1])) == 'Y({1})'
assert str(Y([1])) == 'Y({1})'
assert repr(E()) == 'E()'
assert repr(Z([3])) == 'Z({3})'
assert repr(Z()) == 'Z()'

assert repr(F([1])) == 'F({1})'
assert repr(F(['x'])) == "F({'x'})"
assert str(F(['x'])) == "F({'x'})"
assert repr(F()) == 'F()'

# the builtin forms stay untouched
assert repr({1, 2}) == '{1, 2}'
assert repr(set()) == 'set()'
assert repr(frozenset([1])) == 'frozenset({1})'
assert repr(frozenset()) == 'frozenset()'

# negative controls: other containers have no subclass-name rule
class X(dict):
    pass


class L(list):
    pass


class T(tuple):
    pass


assert repr(X()) == '{}'
assert repr(L([1])) == '[1]'
assert repr(T((1, 2))) == '(1, 2)'

# nesting keeps the inner reprs
assert repr([Y([1, 2]), frozenset([3])]) == '[Y({1, 2}), frozenset({3})]'

# subclass behavior (bool/iteration/len) is unaffected
assert bool(Y()) is False
assert len(Y([1, 2])) == 2
assert sorted(Y([2, 1])) == [1, 2]

print("test_set_subclass_repr_regression passed")
