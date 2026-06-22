"""Regression tests for list bugs found during code review."""

print("=== Test 1: list == non_list should not crash ===")
try:
    result = ([1, 2] == (1, 2))
    assert result == False, f"Expected False, got {result}"
    print("  PASS: [1,2] == (1,2) returns False")
except Exception as e:
    print(f"  FAIL: raised {type(e).__name__}: {e}")

print("\n=== Test 2: list != non_list should not crash ===")
try:
    result = ([1, 2] != (1, 2))
    assert result == True, f"Expected True, got {result}"
    print("  PASS: [1,2] != (1,2) returns True")
except Exception as e:
    print(f"  FAIL: raised {type(e).__name__}: {e}")

print("\n=== Test 3: list < non_list should not crash ===")
try:
    result = ([1, 2] < (1, 2, 3))
    print(f"  [1,2] < (1,2,3) = {result}")
except TypeError as e:
    print(f"  TypeError: {e} (CPython behavior)")
except Exception as e:
    print(f"  FAIL: raised {type(e).__name__}: {e}")

print("\n=== Test 4: list.count with string values ===")
b = ["hello", "world", "hello"]
count = b.count("hello")
assert count == 2, f"count('hello') should be 2, got {count}"
print("  PASS")

s1 = "h" + "ello"
s2 = "wor" + "ld"
s3 = "h" + "ello"
b2 = [s1, s2, s3]
count2 = b2.count("hello")
assert count2 == 2, f"count(dynamic 'hello') should be 2, got {count2}"
print("  PASS (dynamic strings)")

print("\n=== Test 5: list.remove with string values ===")
b = ["hello", "world", "foo"]
b.remove("world")
assert b == ["hello", "foo"], f"expected ['hello', 'foo'], got {b}"
print("  PASS")

s1 = "hel" + "lo"
s2 = "wor" + "ld"
s3 = "fo" + "o"
b3 = [s1, s2, s3]
b3.remove("world")
assert b3 == ["hello", "foo"], f"expected ['hello', 'foo'], got {b3}"
print("  PASS (dynamic strings)")

print("\n=== Test 6: list comparison with wrong type ===")
for op_name, op in [('>=', lambda a,b: a >= b), ('<=', lambda a,b: a <= b),
                      ('>', lambda a,b: a > b), ('<', lambda a,b: a < b)]:
    try:
        result = op([1, 2], "abc")
        print(f"  [1,2] {op_name} 'abc' = {result}")
    except TypeError as e:
        msg = str(e)
        print(f"  [1,2] {op_name} 'abc' -> TypeError (truncated): {len(msg)} chars")
    except Exception as e:
        print(f"  [1,2] {op_name} 'abc' -> FAIL: {type(e).__name__}: {e}")

print("\n=== All tests completed ===")
