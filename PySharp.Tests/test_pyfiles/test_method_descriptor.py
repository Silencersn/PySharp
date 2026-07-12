"""
Tests for method descriptors (bound/unbound methods)
"""

# Test bound method behavior (instance methods)
lst = [1, 2, 3]
assert lst.__repr__() == '[1, 2, 3]'
assert lst.__bool__() is True
assert lst.__str__() == '[1, 2, 3]'

lst2 = lst.copy()
lst.append(4)
assert lst == [1, 2, 3, 4]
assert lst2 == [1, 2, 3]

lst.extend([5, 6])
assert lst == [1, 2, 3, 4, 5, 6]

lst.insert(0, 0)
assert lst[0] == 0
lst.remove(0)
assert 0 not in lst

popped = lst.pop()
assert popped == 6

temp = lst.copy()
temp.clear()
assert temp == []

idx = lst.index(3)
assert lst[idx] == 3
assert lst.count(1) == 1

# Test sorting and reversing methods
sort_test = [3, 1, 4, 1, 5]
sort_test.sort()
assert sort_test == [1, 1, 3, 4, 5]

reverse_test = [1, 2, 3]
reverse_test.reverse()
assert reverse_test == [3, 2, 1]

# Test unbound method behavior (calling via class)
lst = [1, 2, 3]
list.append(lst, 4)
assert lst == [1, 2, 3, 4]

list.extend(lst, [5, 6])
assert lst == [1, 2, 3, 4, 5, 6]

list.insert(lst, 0, 0)
assert lst[0] == 0

result = list.pop(lst, 0)
assert result == 0
assert lst[0] == 1

assert list.count(lst, 2) == 1
index = list.index(lst, 3)
assert lst[index] == 3

list.remove(lst, 2)
assert 2 not in lst

copied2 = list.copy(lst)
lst.append(100)
assert copied2 != lst

# Comparison between instance and unbound result
test_list = [1, 2, 3]
test_list.append(4)
instance_result = test_list.copy()

test_list = [1, 2, 3]
list.append(test_list, 4)
unbound_result = test_list.copy()
assert instance_result == unbound_result

# Test method type construction via New
def my_func(self, x):
    return x * 2

class MyClass:
    pass

obj = MyClass()

# Create a bound method and get its type
bound = my_func.__get__(obj, MyClass)
method_type = type(bound)

# The constructed method should work
m = method_type(my_func, obj)
assert m(21) == 42
assert m.__self__ is obj
assert m.__func__ is my_func

# Test error cases
try:
    method_type("not callable", obj)
    assert False, "Should raise TypeError"
except TypeError:
    pass

try:
    method_type(my_func, None)
    assert False, "Should raise TypeError"
except TypeError:
    pass

print("test_method_descriptor passed")
