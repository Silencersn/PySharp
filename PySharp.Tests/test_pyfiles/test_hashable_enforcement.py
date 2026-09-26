"""Mutable containers are unhashable: hash() raises "unhashable type" and dict-key/set-element paths re-raise with the container wording embedding the original error.

Also pins a class defining __eq__ without __hash__ (or with __hash__ = None) as explicitly unhashable, and that assigning or deleting __hash__ rewires the slot through the MRO.

:kind: test
"""

# Hashability enforcement: mutable containers (list/dict/set/dict_keys/
# dict_items) mark themselves unhashable like CPython's
# PyObject_HashNotImplemented — hash() raises "unhashable type", dict key
# and set element paths re-raise with the container wording embedding the
# original error, tuple hashing propagates element failures, a class
# defining __eq__ without __hash__ (or with __hash__ = None) is explicitly
# unhashable with None in the type dict, and deleting an explicit __hash__
# re-inherits through the MRO.

def expect(label, fn, exc, msg):
    try:
        fn()
    except exc as e:
        assert str(e) == msg, f"{label}: {str(e)!r} != {msg!r}"
    else:
        assert False, f"{label}: no {exc.__name__}"


def hashable(x):
    hash(x)


# hash() direct call
expect("hash([1])", lambda: hash([1]), TypeError, "unhashable type: 'list'")
expect("hash({})", lambda: hash({}), TypeError, "unhashable type: 'dict'")
expect("hash({1})", lambda: hash({1}), TypeError, "unhashable type: 'set'")
expect("hash(bytearray(b'a'))", lambda: hash(bytearray(b'a')), TypeError, "unhashable type: 'bytearray'")
expect("hash((1, [2]))", lambda: hash((1, [2])), TypeError, "unhashable type: 'list'")
expect("hash({}.keys())", lambda: hash({}.keys()), TypeError, "unhashable type: 'dict_keys'")
expect("hash({}.items())", lambda: hash({}.items()), TypeError, "unhashable type: 'dict_items'")

# hashable faces stay hashable
hash(1)
hash("a")
hash(frozenset({1}))
hash(slice(1))
hash(memoryview(b"ab"))
hash({}.values())
assert isinstance(hash((1, (2, "x"))), int)

# __hash__ read faces
assert list.__hash__ is None
assert list().__hash__ is None
assert bytearray.__hash__ is None
assert tuple.__hash__ is not None

# dict key paths (write / read / in / get / setdefault / literal / update /
# fromkeys / tuple key), including the empty-dict read face
expect("store", lambda: {}.__setitem__([1], 0), TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("read", lambda: {}[[1, 2]], TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("in", lambda: [1] in {1: "a"}, TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("get", lambda: {}.get([1]), TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("setdefault", lambda: {}.setdefault([1], "x"), TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("literal", lambda: {[1]: "x"}, TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("update", lambda: {}.update({[1]: 2}), TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("fromkeys", lambda: dict.fromkeys([[1]]), TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")
expect("tuple key", lambda: {}.__setitem__((1, [2]), "v"), TypeError,
       "cannot use 'tuple' as a dict key (unhashable type: 'list')")

# dict.pop: the empty-table fast path precedes hashing; on a non-empty
# dict hash errors surface exactly as for delitem (TypeError keeps the
# container wording, anything else propagates raw), and the default only
# ever swallows KeyError — never the hash error itself
expect("empty pop unhashable", lambda: {}.pop([1]), KeyError, "[1]")
assert {}.pop([1], "d") == "d"
expect("nonempty pop unhashable", lambda: {1: "a"}.pop([1]), TypeError,
       "cannot use 'list' as a dict key (unhashable type: 'list')")


class VE:
    def __eq__(self, other):
        return False

    def __hash__(self):
        raise ValueError("vboom")


expect("nonempty pop raw ValueError", lambda: {1: "a"}.pop(VE()), ValueError, "vboom")
expect("pop default does not swallow", lambda: {1: "a"}.pop(VE(), "d"), ValueError, "vboom")
assert {}.pop(VE(), "d") == "d"

# non-TypeError hash errors skip the container wording everywhere
expect("dict contains raw", lambda: VE() in {}, ValueError, "vboom")
expect("dict get raw", lambda: {}.get(VE()), ValueError, "vboom")
expect("dict insert raw", lambda: {}.__setitem__(VE(), 1), ValueError, "vboom")

# set element paths
expect("set literal", lambda: {[1]}, TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("set add", lambda: set().add([2]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("set in", lambda: [1] in {1}, TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("set comp", lambda: {x for x in [[1]]}, TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("set update", lambda: set().update([[1]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("frozenset()", lambda: frozenset([[1]]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("frozenset in", lambda: [1] in frozenset(), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("set remove", lambda: set().remove([1]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")
expect("set discard", lambda: set().discard([1]), TypeError,
       "cannot use 'list' as a set element (unhashable type: 'list')")

# a custom hash error is embedded verbatim
class Z:
    def __hash__(self):
        raise TypeError("boom")

expect("custom hash dict", lambda: {}.__setitem__(Z(), 1), TypeError,
       "cannot use 'Z' as a dict key (boom)")
expect("custom hash set", lambda: Z() in {1}, TypeError,
       "cannot use 'Z' as a set element (boom)")
try:
    hash(Z())
    assert False, "TypeError expected"
except TypeError as e:
    assert str(e) == "boom"


# a class defining __eq__ without __hash__ is explicitly unhashable
class J:
    def __eq__(self, other):
        return False

expect("hash(J())", lambda: hash(J()), TypeError, "unhashable type: 'J'")
assert J.__hash__ is None
assert J().__hash__ is None
assert "__hash__" in vars(J)
expect("J as dict key", lambda: {J(): "v"}, TypeError,
       "cannot use 'J' as a dict key (unhashable type: 'J')")


# nested classes report the bare name
class Outer:
    class Inner:
        def __eq__(self, other):
            return False

expect("hash(Inner())", lambda: hash(Outer.Inner()), TypeError,
       "unhashable type: 'Inner'")


# an explicit __hash__ = None lands in the class dict
class E:
    def __eq__(self, other):
        return False
    __hash__ = None

expect("hash(E())", lambda: hash(E()), TypeError, "unhashable type: 'E'")
assert E.__hash__ is None
assert "__hash__" in vars(E)


# a subclass defining __hash__ restores hashing with its own function
class H(J):
    def __hash__(self):
        return 42

assert hash(H()) == 42


# assigning __hash__ after creation rewires the slot
class M:
    pass

hash(M())
M.__hash__ = None
expect("M.__hash__ = None", lambda: hash(M()), TypeError, "unhashable type: 'M'")
M.__hash__ = lambda self: 7
assert hash(M()) == 7

# deleting an explicit __hash__ re-inherits through the MRO
del J.__hash__
hash(J())
del M.__hash__
hash(M())

# a subclass made after the delete inherits the restored hashability
class K(J):
    pass

hash(K())

# a subclass of a still-unhashable class stays unhashable without its own __hash__
class U:
    def __eq__(self, other):
        return False

class K2(U):
    pass

expect("hash(K2())", lambda: hash(K2()), TypeError, "unhashable type: 'K2'")

print("test_hashable_enforcement passed")
