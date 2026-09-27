"""A tuple display of constant elements is one compile-time constant, so
equal constant displays are the same object.

CPython folds a constant tuple display into a single co_consts entry, and
two displays with equal constants share that entry; PySharp does the same
rather than building a fresh tuple on every evaluation. Element identity is
type-sensitive: (1,) and (True,) are equal but distinct constants, as are
(0.0,) and (-0.0,), (1,) and (1+0j,), and (b"a",) and ("a",). A display
that contains a runtime value is not constant at all and keeps building a
fresh tuple. Value semantics are untouched throughout: only `is` sees the
difference, and `==` still reports the two displays equal.

:kind: test
:background: CPython folds a constant tuple display into one LOAD_CONST
    (Python/codegen.c codegen_tuple and Python/flowgraph.c
    fold_tuple_of_constants) and keys its const cache by
    _PyCode_ConstantKey (Objects/codeobject.c), which folds the element
    types into the key so int/bool/complex/bytes/signed-zero never
    collapse. The empty tuple is a singleton either way, so it is not
    evidence of folding.
"""

# A constant display is one constant: equal displays are the same object
# within the code object where they appear.
assert (1,) is (1,)
assert (1, 2) is (1, 2)
assert (1, 2, 3) is (1, 2, 3)
assert (1.5,) is (1.5,)
assert (b"a",) is (b"a",)
assert (1 + 2j,) is (1 + 2j,)
assert (...,) is (...,)
assert (None,) is (None,)
assert (True,) is (True,)
assert ("ab",) is ("ab",)
assert (10 ** 30,) is (10 ** 30,)

# Nested displays fold through the inner tuples.
assert ((1, 2),) is ((1, 2),)
assert ((1, 2), (3,)) is ((1, 2), (3,))

# A display built from constant subexpressions folds too, since the
# elements fold first.
assert (1 + 2,) is (3,)
assert (1 + 2, 3 * 4) is (3, 12)

# An element's type is part of the constant's identity, so these are all
# equal by == but never the same object.
assert (1,) == (True,)
assert not ((1,) is (True,))
assert (1,) == (1 + 0j,)
assert not ((1,) is (1 + 0j,))
assert (1,) == (1.0,)
assert not ((1,) is (1.0,))
assert (b"a",) != ("a",)
assert not ((b"a",) is ("a",))
assert (0.0,) == (-0.0,)
assert not ((0.0,) is (-0.0,))
assert ((1,),) == ((True,),)
assert not (((1,),) is ((True,),))


def local_display():
    # The display and the bound name share the one constant inside this
    # code object.
    a = (1, 2)
    return a is (1, 2)


assert local_display() is True


def displays_in_one_body():
    return (1,) is (1,)


assert displays_in_one_body() is True


# A display with a runtime element is not constant; each evaluation builds
# a fresh tuple, so it is not the same object even while == holds.
y = 1
assert (y, 2) is not (y, 2)
assert (y, 2) == (y, 2)
assert (y,) is not (y,)

# A starred element makes the display runtime-built as well.
items = [1, 2]
assert (*items,) == (1, 2)


def runtime_element(y):
    assert (y, 1, 2) is not (y, 1, 2)


runtime_element(1)

# The empty tuple is a singleton, so it compares identical on its own
# terms rather than through folding.
assert () is ()
assert len(()) == 0

# Value semantics are unaffected by folding.
t = (1, 2, 3)
assert t[0] == 1
assert t[-1] == 3
assert len(t) == 3
assert t == (1, 2, 3)
assert list(t) == [1, 2, 3]
a, b, c = t
assert (a, b, c) == (1, 2, 3)
assert (1,) + (2,) == (1, 2)
assert (1,) * 3 == (1, 1, 1)


def default_display(x=(1, 2)):
    return x


assert default_display() == (1, 2)

print("tuple constant display identity ok")
