# test_open: Verify open() builtin function

# Test 1: Write text file
print("=== Test 1: write text ===")
f = open("_test_open_out.txt", "w")
result = f.write("hello world")
print(f"write returned: {result}")
f.close()
print(f"closed: {f.closed}")
print("OK: write text")

# Test 2: Read text file
print()
print("=== Test 2: read text ===")
f = open("_test_open_out.txt", "r")
data = f.read()
print(f"read: {data}")
assert data == "hello world", f"Expected 'hello world', got '{data}'"
f.close()
print("OK: read text")

# Test 3: Context manager
print()
print("=== Test 3: context manager ===")
with open("_test_open_out.txt", "r") as f:
    data = f.read()
    print(f"with read: {data}")
assert f.closed, "File should be closed after with block"
print("OK: context manager")

# Test 4: Binary mode
print()
print("=== Test 4: binary mode ===")
data = bytes([0, 1, 2, 255, 128])
with open("_test_open_bin.bin", "wb") as f:
    f.write(data)
with open("_test_open_bin.bin", "rb") as f:
    readback = f.read()
assert readback == data, f"Binary mismatch: {readback} != {data}"
print("OK: binary mode")

# Test 5: Append mode
print()
print("=== Test 5: append mode ===")
with open("_test_open_append.txt", "w") as f:
    f.write("first")
with open("_test_open_append.txt", "a") as f:
    f.write("second")
with open("_test_open_append.txt", "r") as f:
    content = f.read()
print(f"append result: '{content}'")
assert content == "firstsecond", f"Expected 'firstsecond', got '{content}'"
print("OK: append mode")

# Test 6: FileNotFoundError
print()
print("=== Test 6: FileNotFoundError ===")
try:
    open("_nonexistent_file_xyz.txt", "r")
    assert False, "Should raise FileNotFoundError"
except FileNotFoundError as e:
    print(f"Got FileNotFoundError: {e}")
print("OK: FileNotFoundError")

# Test 7: readline
print()
print("=== Test 7: readline ===")
with open("_test_open_out.txt", "w") as f:
    f.write("line1\nline2\nline3\n")
with open("_test_open_out.txt", "r") as f:
    line1 = f.readline()
    print(f"line1: '{line1}'")
    assert line1 == "line1\n", f"Expected 'line1\\n', got '{line1}'"
    line2 = f.readline()
    print(f"line2: '{line2}'")
    assert line2 == "line2\n", f"Expected 'line2\\n', got '{line2}'"
print("OK: readline")

# Test 8: seek and tell
print()
print("=== Test 8: seek/tell ===")
with open("_test_open_out.txt", "r") as f:
    f.seek(0)
    data = f.read(5)
    print(f"read(5) at start: '{data}'")
    assert data == "line1", f"Expected 'line1', got '{data}'"
    f.seek(6)
    pos = f.tell()
    print(f"tell after seek(6): {pos}")
    assert pos == 6, f"Expected pos 6, got {pos}"
    data = f.read(4)
    print(f"read(4) at pos 6: '{data}'")
    assert data == "line", f"Expected 'line', got '{data}'"
print("OK: seek/tell")

print()
print("All open tests passed")
