# Test memoryview basic behavior
# Verify against CPython behavior

# ===== Basic construction =====
mv = memoryview(b'hello')
assert mv[0] == 104  # ord('h')
assert mv[1] == 101  # ord('e')
assert len(mv) == 5
assert mv.readonly == True
assert mv.format == 'B'
assert mv.itemsize == 1
assert mv.ndim == 1
print("basic construction: OK")

# ===== tobytes / tolist / hex =====
result1 = mv.tobytes()
assert result1 == b'hello'
print("tobytes: OK")

result2 = mv.tolist()
assert result2[0] == 104
assert result2[1] == 101
assert len(result2) == 5
print("tolist: OK")

# ===== Slice =====
sub = mv[1:4]
result3 = sub.tobytes()
assert result3 == b'ell'
print("slice: OK")

# ===== Empty =====
empty_mv = memoryview(b'')
assert len(empty_mv) == 0
print("empty: OK")

# ===== Iter =====
count = 0
for b in memoryview(b'abc'):
    count = count + 1
assert count == 3
print("iteration: OK")

# ===== Comparison =====
mv_a = memoryview(b'abc')
mv_b = memoryview(b'abc')
mv_c = memoryview(b'abd')
assert mv_a == mv_b
assert mv_a != mv_c
print("comparison: OK")

# ===== memoryview from memoryview =====
mv2 = memoryview(mv)
result4 = mv2.tobytes()
assert result4 == b'hello'
print("memoryview from memoryview: OK")

# ===== Release =====
mv4 = memoryview(b'test')
mv4.release()
try:
    mv4.tobytes()
    assert False
except ValueError:
    pass
print("release: OK")

# ===== Type errors =====
try:
    memoryview(42)
    assert False
except TypeError:
    pass

try:
    memoryview("string")
    assert False
except TypeError:
    pass
print("type errors: OK")

print("ALL TESTS PASSED")
