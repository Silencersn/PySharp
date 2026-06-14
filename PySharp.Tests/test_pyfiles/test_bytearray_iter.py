"""
Isolated test for bytearray iteration - no other tests in this file
"""
print("starting bytearray iteration test")
ba = bytearray(b"ABC")
print("bytearray created, len =", len(ba))
it = iter(ba)
print("iter called")
v1 = next(it)
print("first next:", v1)
assert v1 == 65
v2 = next(it)
print("second next:", v2)
assert v2 == 66
v3 = next(it)
print("third next:", v3)
assert v3 == 67
try:
    next(it)
    assert False, "Should raise StopIteration"
except StopIteration:
    print("StopIteration raised correctly")

print("test_bytearray_iter passed")
