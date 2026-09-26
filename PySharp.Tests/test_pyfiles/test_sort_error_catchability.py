"""
sort comparison errors stay ordinary Python exceptions.

Every comparison inside list.sort()/sorted() propagates through the
interpreter's PyResult error plumbing, so a TypeError from incomparable
elements — or any exception raised by a user __lt__ — is catchable by
except TypeError and even except BaseException, on both the
count_run/binarysort path (< 64 elements) and the merge path
(>= 64 elements). The failure message reports the operands in CPython's
order (pivot first: 'NoneType' and 'int' for [1, None].sort()), and dir()
propagates the same way when a user __dir__ yields unorderable entries.

CPython 3.14 reference (Objects/listobject.c list_sort_impl error paths).

:kind: test
"""

# the escape repro: mixed-type comparison is catchable by BaseException
try:
    [1, None].sort()
except BaseException as e:
    assert type(e) is TypeError
    assert str(e) == "'<' not supported between instances of 'NoneType' and 'int'"
else:
    assert False, "TypeError expected"

try:
    sorted([1, None])
except BaseException:
    pass
else:
    assert False, "TypeError expected"

# narrow except still catches, and execution continues
try:
    ["a", 1].sort()
except TypeError:
    pass
else:
    assert False, "TypeError expected"
try:
    sorted([1, "a"])
except TypeError as e:
    assert str(e) == "'<' not supported between instances of 'str' and 'int'"
else:
    assert False, "TypeError expected"

# merge path (>= 64 elements) errors are catchable too
data = list(range(100))
data[70] = None
try:
    data.sort()
except BaseException as e:
    assert type(e) is TypeError
else:
    assert False, "TypeError expected"
try:
    sorted(list(range(99)) + [None])
except TypeError:
    pass
else:
    assert False, "TypeError expected"

# a user __lt__ raising escapes nothing, in-place and via sorted
class Boom:
    def __lt__(self, o):
        raise RuntimeError("boom-lt")
try:
    [Boom(), Boom()].sort()
except BaseException as e:
    assert type(e) is RuntimeError and str(e) == "boom-lt"
else:
    assert False, "RuntimeError expected"
try:
    sorted([Boom(), Boom()])
except RuntimeError as e:
    assert str(e) == "boom-lt"
else:
    assert False, "RuntimeError expected"

# a raising __lt__ reached through the merge path
class LateBoom:
    def __init__(self, k):
        self.k = k
    def __lt__(self, o):
        if self.k == 999 or o.k == 999:
            raise RuntimeError("boom-merge")
        return self.k < o.k
items = [LateBoom(i) for i in range(64)]
items.insert(10, LateBoom(999))
try:
    items.sort()
except BaseException as e:
    assert type(e) is RuntimeError and str(e) == "boom-merge"
else:
    assert False, "RuntimeError expected"

# a user __dir__ yielding unorderable entries: dir() sorts and propagates
class WeirdDir:
    def __dir__(self):
        return ["b", 1, "a"]
try:
    dir(WeirdDir())
except TypeError:
    pass
else:
    assert False, "TypeError expected"

# control group: min/max comparison errors were always catchable
try:
    min([1, None])
except TypeError:
    pass
else:
    assert False, "TypeError expected"
try:
    max([1, None])
except TypeError:
    pass
else:
    assert False, "TypeError expected"

print("test_sort_error_catchability passed")
