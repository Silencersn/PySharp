"""
zip() with no arguments must be an exhausted iterator
(CPython zip_next never yields the empty tuple). It previously returned
() once before stopping, so list(zip()) was [()].

:kind: test
"""

# no iterables: immediately exhausted
assert list(zip()) == []
z = zip()
try:
    next(z)
except StopIteration:
    pass
else:
    raise AssertionError("next(zip()) yielded the empty tuple")

count = 0
for t in zip():
    count += 1
assert count == 0

# regular arities are unaffected
assert list(zip([], [])) == []
assert list(zip([1])) == [(1,)]
assert list(zip([1, 2], [3])) == [(1, 3)]
assert list(zip([1], [2], [3])) == [(1, 2, 3)]
assert list(zip(*[[1, 2], [3, 4]])) == [(1, 3), (2, 4)]
assert list(zip('ab', (1, 2))) == [('a', 1), ('b', 2)]

# strict mode still reports mismatches
try:
    list(zip([1, 2], [3], strict=True))
except ValueError as e:
    assert str(e) == "zip() argument 2 is shorter than argument 1"
else:
    raise AssertionError("strict mismatch silent")

print("test_zip_no_args passed")
