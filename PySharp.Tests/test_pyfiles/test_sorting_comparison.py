"""
Sorting and comparison tests - exercises comparison operations and edge cases
"""

# Comparison chaining
assert 1 < 2 < 3
assert 1 < 2 <= 2
assert 3 > 2 > 1
assert 3 >= 3 > 2

# Compare different numeric types
assert 1 == 1.0
assert 1 < 1.5
assert 2.5 > 2

# Tuple comparison
assert (1, 2) < (1, 3)
assert (1, 2) == (1, 2)
assert (2, 1) > (1, 5)
assert (1, 2, 3) < (2, 1)

# List comparison
assert [1, 2] < [1, 3]
assert [1, 2, 3] > [1, 2]

# String comparison
assert 'a' < 'b'
assert 'abc' < 'abd'
assert 'hello' == 'hello'
assert 'hello' != 'world'

# Boolean comparison
assert True == True
assert False == False
assert True != False
assert False < True

# None comparison raises TypeError in Python 3
try:
    None < 1
    assert False, "None comparison should raise TypeError"
except TypeError:
    pass

# Incompatible types
try:
    1 < 'a'
    assert False, "Should raise TypeError"
except TypeError:
    pass

# min/max edge cases
assert min(5, 2, 8, 1, 9) == 1
assert max(5, 2, 8, 1, 9) == 9
assert min('banana', 'apple', 'cherry') == 'apple'
assert max('banana', 'apple', 'cherry') == 'cherry'

# Custom object comparison (direct, not sorted)
class OrderedItem:
    def __init__(self, value):
        self.value = value
    def __lt__(self, other):
        return self.value < other.value
    def __eq__(self, other):
        if not isinstance(other, OrderedItem):
            return False
        return self.value == other.value

a = OrderedItem(5)
b = OrderedItem(2)
c = OrderedItem(8)
assert (a < b) is False
assert (b < a) is True
assert (a == a) is True
assert (a == b) is False

print("test_sorting_comparison passed")
