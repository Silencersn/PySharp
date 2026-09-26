"""
Tests for zip and enumerate built-ins

:kind: test
"""

# Test enumerate
lst = ['a', 'b', 'c']
# enumerate result is an iterator, converting to list for assertion
enum_results = list(enumerate(lst))
assert enum_results == [(0, 'a'), (1, 'b'), (2, 'c')]

enum_start = list(enumerate(lst, 10))
assert enum_start == [(10, 'a'), (11, 'b'), (12, 'c')]

# Test zip
lst1 = [1, 2, 3]
lst2 = ['x', 'y', 'z']
zip_results = list(zip(lst1, lst2))
assert zip_results == [(1, 'x'), (2, 'y'), (3, 'z')]

print("test_zip_enumerate passed")
