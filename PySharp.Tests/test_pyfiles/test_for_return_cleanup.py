"""
Regression test: for-loop iterator cleanup on return.

Tests that returning from inside a for-loop body properly cleans up
the iterator from the operand stack, preventing the debug-mode
"Stack.Count is greater than 0" assertion.
"""

# Test 1: simple for-loop with return
def first(items):
    for x in items:
        return x
    return None

assert first([10, 20, 30]) == 10, "Should return first item"
# Verify the function still works on subsequent calls
assert first([42]) == 42

# Test 2: nested for-loops with return from inner loop
def find_pair(matrix, target):
    for row in matrix:
        for item in row:
            if item == target:
                return (row[0], item)
    return None

result = find_pair([[1, 2], [3, 4]], 3)
assert result == (3, 3), f"Expected (3, 3), got {result}"
assert find_pair([[1, 2], [3, 4]], 5) is None

# Test 3: for-loop with conditional return in middle
def first_positive(items):
    for x in items:
        if x > 0:
            return x
    return -1

assert first_positive([-5, -3, 7, 2]) == 7
assert first_positive([-1, -2]) == -1
assert first_positive([0, 0, 1]) == 1

# Test 4: for-loop with return inside try-finally (ensure still works)
def lookup(items, key):
    for item in items:
        try:
            if item == key:
                return item
        finally:
            pass  # ensure finally doesn't interfere
    return None

assert lookup([1, 2, 3], 2) == 2
assert lookup([1, 2, 3], 99) is None

# Test 5: multiple sequential for-loops with returns
def find_first_two(lists):
    first = None
    second = None
    for lst in lists:
        for x in lst:
            first = x
            break
        break
    for lst in lists:
        for x in lst:
            second = x
            break
        break
    return (first, second)

result = find_first_two([[10, 20], [30, 40]])
assert result == (10, 10), f"Expected (10, 10), got {result}"

print("test_for_return_cleanup passed")
