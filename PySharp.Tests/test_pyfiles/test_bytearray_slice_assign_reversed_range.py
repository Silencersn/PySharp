"""A reversed range in bytearray step-1 slice assignment (stop < start) is
normalized to a plain insertion at start — nothing is deleted, so
b'abcdef'[3:1] = b'XY' yields b'abcXYdef' and [3:1] = b'' is a no-op. With a
live memoryview export the same assignment refuses as any resize.

:kind: test
:background: CPython bytearray_ass_subscript normalizes the slice indices
    before deleting (Objects/bytearrayobject.c: stop < start collapses to
    start), so the deletion span is empty and the value inserts at start.
"""

b = bytearray(b"abcdef")
b[3:1] = b"XY"
assert bytes(b) == b"abcXYdef", bytes(b)

b[3:1] = b""
assert bytes(b) == b"abcXYdef", bytes(b)

b[1:1] = b"Z"
assert bytes(b) == b"aZbcXYdef", bytes(b)

mv = memoryview(b)
try:
    b[4:2] = b"Q"
except BufferError as e:
    assert str(e) == "Existing exports of data: object cannot be re-sized"
else:
    raise AssertionError("reversed assignment with live export must raise BufferError")

mv.release()
b[4:2] = b"Q"
assert bytes(b) == b"aZbcQXYdef", bytes(b)

print("test_bytearray_slice_assign_reversed_range: OK")
