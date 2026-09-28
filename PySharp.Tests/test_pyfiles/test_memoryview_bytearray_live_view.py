"""memoryview shares live storage with its bytearray exporter: writes through
the view land in the bytearray, bytearray mutations are observed by the view,
and every read face (indexing, bytes(), tobytes(), tolist(), iteration,
equality) sees the same current bytes.

Slicing yields a subview over the same exporter without copying; its writes
land at the shifted offset. memoryview(memoryview) re-exports the same buffer,
so .obj stays the original bytearray and both views stay in sync.

:kind: test
:background: The buffer protocol contract is a live view of the exporter's
    storage (CPython Objects/bytearrayobject.c exports the internal buffer;
    Py_buffer.obj addresses it). PySharp previously handed out a
    construction-time snapshot copy, desynchronizing view and exporter.
"""

b = bytearray(b"abcdef")
mv = memoryview(b)

# single-item writes go through to the exporter
mv[0] = 65
assert bytes(b) == b"Abcdef", bytes(b)
assert bytes(mv) == b"Abcdef"

# exporter item writes are observed by the view
b[1] = 66
assert mv[1] == 66, mv[1]
assert bytes(mv) == b"ABcdef"
assert b"AB" == mv[:2].tobytes()

# every read face agrees on the current bytes
assert mv.tolist() == [65, 66, 99, 100, 101, 102]
assert mv.hex() == "414263646566"
assert 66 in mv
assert b"ABc" == bytes(mv[:3])
count = 0
seen = []
for item in mv:
    seen.append(item)
    count = count + 1
assert count == 6
assert seen == [65, 66, 99, 100, 101, 102]

# slice write-through, including strided targets
mv[:3] = b"XYZ"
assert bytes(b) == b"XYZdef", bytes(b)
mv[0:6:2] = b"xyz"
assert bytes(b) == b"xYydzf", bytes(b)

# a subview writes through to the same exporter at its offset
sub = mv[1:4]
assert sub.obj is b, "subview .obj must be the original exporter"
sub[0] = 66
assert bytes(b) == b"xBydzf", bytes(b)
assert sub == b"Byd"

# view of a view stays synchronized with the exporter
nested = memoryview(mv)
assert nested.obj is b
b[5] = 70
assert nested[5] == 70
assert nested == mv

# views over immutable bytes keep reading the same storage
sb = memoryview(b"hello")
assert sb[0] == 104
assert sb == b"hello"

print("test_memoryview_bytearray_live_view: OK")
