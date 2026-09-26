"""
Standard set operations and behavior tests

:kind: test
"""

# Set creation
s = {1, 2, 3, 2, 1}
assert 1 in s
assert 4 not in s

# Set support is limited to creation and containment in PySharp
# len, add, remove, and iteration are not yet implemented

print("test_set passed")
