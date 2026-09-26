"""
the length-hint protocol.

CPython's PyObject_LengthHint consults len() first (only its TypeError falls
through to the hint) and then __length_hint__ on the type's MRO — instance
attributes are ignored, descriptor binding applies. Only TypeError from the
hint call falls back; every other exception propagates, which is what makes
user errors inside __length_hint__ observable. Iteration starts before the
hint is consulted, so a failing __iter__ wins over a failing hint.

The hint is consumed by the list() family (list()/__init__/extend/+=/[*x]/
sorted() and a custom iterator handed to list()), bytes(iterable),
bytearray.extend and operator.length_hint — but NOT by tuple()/set()/dict()
construction, dict.update, set.update, the bytearray() constructor, or
bytearray += (which requires a bytes-like object). Hint value validation:
non-int → TypeError, negative → ValueError, >= 2**63 → OverflowError, beyond
allocatable size → MemoryError (CPython's failed preallocation).

CPython 3.14 reference (Objects/abstract.c PyObject_LengthHint,
Objects/listobject.c list_extend, Objects/bytesobject.c _PyBytes_FromIterator,
Objects/bytearrayobject.c bytearray_extend, Modules/_operator.c length_hint).

Built-in iterator __length_hint__ providers (list_iterator and friends) are a
separate parity gap and intentionally not covered here.

:kind: test
"""

# --- the reported case: a user error inside __length_hint__ propagates ---
class HintRaise:
    def __init__(self, items):
        self._it = iter(items)

    def __iter__(self):
        return self._it

    def __length_hint__(self):
        raise RuntimeError("hint-boom")

try:
    list(HintRaise([1, 2, 3]))
    assert False, "RuntimeError must propagate out of list()"
except RuntimeError as e:
    assert str(e) == "hint-boom", str(e)

# --- TypeError from the hint call falls back (both shapes) ---
class HintTypeError(HintRaise):
    def __length_hint__(self):
        raise TypeError("inner-te")

assert list(HintTypeError([1, 2])) == [1, 2]

class HintNonCallable:
    def __iter__(self):
        return iter([3])
    __length_hint__ = 5

assert list(HintNonCallable()) == [3]

# --- a valid hint and the NotImplemented fallback ---
class HintValue(HintRaise):
    def __length_hint__(self):
        return 5

assert list(HintValue([7, 8])) == [7, 8]

class HintNotImpl(HintRaise):
    def __length_hint__(self):
        return NotImplemented

assert list(HintNotImpl([9])) == [9]

# --- hint value validation ---
class HintStr(HintRaise):
    def __length_hint__(self):
        return "abc"

try:
    list(HintStr([1]))
    assert False
except TypeError as e:
    assert str(e) == "__length_hint__ must be an integer, not str", str(e)

class HintFloat(HintRaise):
    def __length_hint__(self):
        return 5.0

try:
    list(HintFloat([1]))
    assert False
except TypeError as e:
    assert str(e) == "__length_hint__ must be an integer, not float", str(e)

class HintBool(HintRaise):
    def __length_hint__(self):
        return True

assert list(HintBool([1])) == [1]

class HintNeg(HintRaise):
    def __length_hint__(self):
        return -1

try:
    list(HintNeg([1]))
    assert False
except ValueError as e:
    assert str(e) == "__length_hint__() should return >= 0", str(e)

class HintHuge(HintRaise):
    def __length_hint__(self):
        return 2 ** 63

try:
    list(HintHuge([1]))
    assert False
except OverflowError as e:
    assert str(e) == "Python int too large to convert to C ssize_t", str(e)

class HintBig(HintRaise):
    def __length_hint__(self):
        return 10 ** 12

try:
    list(HintBig([1]))
    assert False
except MemoryError:
    pass

# --- len() wins when available; only its TypeError falls through ---
class LenTypeErrorHintRaise(HintRaise):
    def __len__(self):
        raise TypeError("len-te")

try:
    list(LenTypeErrorHintRaise([4]))
    assert False
except RuntimeError as e:
    assert str(e) == "hint-boom", str(e)

class LenTypeErrorNoHint:
    def __iter__(self):
        return iter([4])
    def __len__(self):
        raise TypeError("len-te")

assert list(LenTypeErrorNoHint()) == [4]

class LenValueErrorHint:
    def __iter__(self):
        return iter([4])
    def __len__(self):
        raise ValueError("len-ve")
    def __length_hint__(self):
        return 1

try:
    list(LenValueErrorHint())
    assert False
except ValueError as e:
    assert str(e) == "len-ve", str(e)

# --- instance attributes are ignored ---
class HintInstanceOnly:
    def __iter__(self):
        return iter([1])
    def __init__(self):
        self.__length_hint__ = lambda: 99

assert list(HintInstanceOnly()) == [1]

# --- iteration starts first: a failing __iter__ wins over a failing hint ---
class BrokenIterHintRaise:
    def __iter__(self):
        raise ValueError("iter-boom")
    def __length_hint__(self):
        raise RuntimeError("hint-boom3")

try:
    list(BrokenIterHintRaise())
    assert False
except ValueError as e:
    assert str(e) == "iter-boom", str(e)

# --- the getitem-only iteration protocol consumes the hint too ---
class GetItemOnly:
    def __getitem__(self, i):
        if i >= 2:
            raise IndexError
        return i
    def __length_hint__(self):
        raise RuntimeError("seq-boom")

try:
    list(GetItemOnly())
    assert False
except RuntimeError as e:
    assert str(e) == "seq-boom", str(e)

# --- the other list-side consumers ---
try:
    [].extend(HintRaise([1]))
    assert False
except RuntimeError as e:
    assert str(e) == "hint-boom", str(e)

l = []
try:
    l += HintRaise([1])
    assert False
except RuntimeError:
    pass
assert l == [], "in-place concat must leave the list unchanged on failure"

try:
    [*HintRaise([1])]
    assert False
except RuntimeError:
    pass

try:
    sorted(HintRaise([2, 1]))
    assert False
except RuntimeError:
    pass

# a custom iterator handed straight to list() is hint-consumed as well
class RaisingHintIterator:
    def __iter__(self):
        return self
    def __next__(self):
        raise StopIteration
    def __length_hint__(self):
        raise RuntimeError("iterhint-boom")

try:
    list(RaisingHintIterator())
    assert False
except RuntimeError as e:
    assert str(e) == "iterhint-boom", str(e)

# --- non-consumers: these must keep silently ignoring the hint ---
assert tuple(HintRaise([1, 2])) == (1, 2)
assert set(HintRaise([1, 2])) == {1, 2}
assert dict(HintRaise([('a', 1)])) == {'a': 1}

d = {}
d.update(HintRaise([('a', 1)]))
assert d == {'a': 1}

s = set()
s.update(HintRaise([1]))
assert s == {1}

assert bytearray(HintRaise([65])) == bytearray(b'A')

# --- bytes() constructor consumes the hint ---
try:
    bytes(HintRaise([65]))
    assert False
except RuntimeError:
    pass

try:
    bytes(HintBig([65]))
    assert False
except MemoryError:
    pass

# --- bytearray.extend consumes the hint; the constructor path does not ---
ba = bytearray()
try:
    ba.extend(HintRaise([65]))
    assert False
except RuntimeError:
    pass

ba2 = bytearray()
try:
    ba2.extend(HintBig([65]))
    assert False
except MemoryError:
    pass

# bytearray += requires a bytes-like object and never reaches the hint
ba3 = bytearray()
try:
    ba3 += HintRaise([65])
    assert False
except TypeError as e:
    assert str(e) == "can't concat HintRaise to bytearray", str(e)

# --- operator.length_hint ---
import operator

assert operator.length_hint([1, 2, 3]) == 3, "len() wins when available"
assert operator.length_hint('abcd', 7) == 4
assert operator.length_hint(3) == 0, "no len and no hint yields the default"
assert operator.length_hint(iter([])) == 0

try:
    operator.length_hint(HintRaise([1]), 5)
    assert False
except RuntimeError:
    pass

try:
    operator.length_hint(HintStr([1]))
    assert False
except TypeError as e:
    assert str(e) == "__length_hint__ must be an integer, not str", str(e)

try:
    operator.length_hint(HintNeg([1]))
    assert False
except ValueError as e:
    assert str(e) == "__length_hint__() should return >= 0", str(e)

try:
    operator.length_hint(3, 2 ** 63)
    assert False
except OverflowError:
    pass

class NoHint:
    pass

assert operator.length_hint(NoHint(), -5) == -5, "an unvalidated default passes through"

# --- inheritance and descriptor binding of __length_hint__ ---
class HintBase:
    def __length_hint__(self):
        return 9

class HintChild(HintBase):
    def __iter__(self):
        return iter([1])

assert list(HintChild()) == [1]
assert operator.length_hint(HintChild()) == 9

class HintProperty:
    def __iter__(self):
        return iter([1])
    __length_hint__ = property(lambda self: lambda: 3)

assert list(HintProperty()) == [1]

class HintZero(HintRaise):
    def __length_hint__(self):
        return 0

assert list(HintZero([1])) == [1]

print("length hint passed")
