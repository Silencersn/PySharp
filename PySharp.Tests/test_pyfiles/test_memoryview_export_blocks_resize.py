"""A live memoryview export blocks every length-changing bytearray operation
with BufferError('Existing exports of data: object cannot be re-sized'), while
same-size writes stay allowed and visible to the view; the lock lifts once
every exporting view is released, release() is idempotent, and the with-
statement releases on exit.

Every view in this fixture is held and released explicitly: PySharp has no
refcounting, so an abandoned (unreferenced but unreleased) view keeps its
export until collected, where CPython would drop it immediately.

:kind: test
:background: CPython bytearray_resize_lock_held short-circuits a same-size
    resize before _canresize checks ob_exports, so extend-by-empty, *1 and
    same-length slice assignment stay legal with live exports
    (Objects/bytearrayobject.c). Subviews and memoryview(memoryview) hold
    their own export; memory_release is idempotent in 3.14.
"""

RESIZE_MESSAGE = "Existing exports of data: object cannot be re-sized"

b = bytearray(b"abcdef")
mv = memoryview(b)


def refuses_resize(operation):
    try:
        operation()
    except BufferError as e:
        return str(e) == RESIZE_MESSAGE
    return False


# every length-changing operation refuses while the export is alive
assert refuses_resize(lambda: b.append(42)), "append must raise BufferError"
assert refuses_resize(lambda: b.extend(b"xx")), "extend must raise BufferError"
assert refuses_resize(lambda: b.__iadd__(b"xx")), "b += must raise BufferError"
assert refuses_resize(lambda: b.__imul__(2)), "b *= 2 must raise BufferError"
assert refuses_resize(lambda: b.__imul__(0)), "b *= 0 must raise BufferError"

# same-size writes stay legal and are observed by the view
b.extend(b"")
b *= 1
b[0:2] = b"XY"
b[:3] = b"ZZZ"
assert bytes(mv) == b"ZZZdef", bytes(mv)
assert len(mv) == 6

# a same-size slice assignment on the view itself writes through
mv[3:5] = b"QQ"
assert bytes(b) == b"ZZZQQf", bytes(b)

# the lock lifts when the view is released, and release is idempotent
mv.release()
mv.release()
b.append(42)
assert bytes(b) == b"ZZZQQf*"

# a subview holds its own export until released, distinct from its parent
ba = bytearray(b"abcdef")
outer = memoryview(ba)
sub = outer[1:4]
assert refuses_resize(lambda: ba.append(1)), "subview export must block resize"
sub.release()
assert refuses_resize(lambda: ba.append(1)), "parent export must still block resize"
outer.release()
ba.append(1)
assert bytes(ba) == b"abcdef\x01"

# nested views keep the lock until every view is released
ba2 = bytearray(b"ab")
outer2 = memoryview(ba2)
inner = memoryview(outer2)
outer2.release()
assert refuses_resize(lambda: ba2.append(1)), "nested export must block resize"
inner.release()
ba2.append(1)
assert bytes(ba2) == b"ab\x01"

# the with-statement releases on exit
held = bytearray(b"xy")
with memoryview(held) as view:
    view[0] = 88
    assert bytes(view.obj) == b"Xy"
held.append(2)
assert bytes(held) == b"Xy\x02"

# a released view no longer serves as a buffer operand: equality falls back
# to identity, concatenation refuses, slice assignment walks the iteration
# path and hits the released error
locked = bytearray(b"abcdef")
doomed = memoryview(locked)
doomed.release()
assert (b"x" == doomed) is False
assert (doomed == b"x") is False
spare = bytearray(b"xy")
try:
    spare += doomed
except TypeError as e:
    assert str(e) == "can't concat memoryview to bytearray", str(e)
else:
    raise AssertionError("+= with released view must raise TypeError")
try:
    spare[0:2] = doomed
except ValueError as e:
    assert str(e) == "operation forbidden on released memoryview object", str(e)
else:
    raise AssertionError("slice assign from released view must raise ValueError")

print("test_memoryview_export_blocks_resize: OK")
