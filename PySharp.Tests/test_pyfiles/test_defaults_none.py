"""
Tests for None default vs no-default distinction in kwonly args.
This should pass once the sentinel bug is fixed.
"""
print("testing defaults none")

# kwonly with None default — should NOT be treated as "no default"
def with_none(*, a=None):
    return a is None

# kwonly with no default — should require the argument
def required(*, a):
    return a

# Test 1: None default, call without arg
result = with_none()
assert result == True, f"Expected True, got {result}"

# Test 2: None default, call with explicit arg
result = with_none(a=42)
assert result == False, f"Expected False, got {result}"

# Test 3: None default, call with explicit None
result = with_none(a=None)
assert result == True, f"Expected True, got {result}"

# Test 4: required kwonly with explicit arg
result = required(a=99)
assert result == 99, f"Expected 99, got {result}"

print("test_defaults_none passed")
