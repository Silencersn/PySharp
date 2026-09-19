"""
Regression: with-block exception exit releases the file handle
synchronously and deterministically — __exit__ runs during unwinding,
the closed flag is set, and the same path can be reopened immediately
(in write mode too, where a lingering handle would collide with OS
sharing rules), including when the exception unwinds out of a function
frame.

CPython 3.14 reference: ceval WITH_EXCEPT_START/POP_EXCEPT call __exit__
synchronously during unwinding; file close is deterministic (no GC
finalizer involvement).
"""

# exception inside the with body, caught outside: __exit__ ran and closed
try:
    with open("_bh_reopen.txt", "w") as f:
        f.write("x")
        raise ValueError("boom")
except ValueError as e:
    assert str(e) == "boom"
assert f.closed

# the same path reopens immediately in write mode after the with-exception
with open("_bh_reopen.txt", "w") as g:
    g.write("y")
with open("_bh_reopen.txt") as r:
    assert r.read() == "y"

# exception unwinding out of a function frame releases the handle too
def leak_into_caller():
    with open("_bh_reopen2.txt", "w") as h:
        h.write("x")
        raise ValueError("b2")
try:
    leak_into_caller()
except ValueError:
    pass
with open("_bh_reopen2.txt", "w") as g2:
    g2.write("y")
with open("_bh_reopen2.txt") as r2:
    assert r2.read() == "y"

# with-normal exit control: reopen write and read back
with open("_bh_reopen3.txt", "w") as f3:
    f3.write("x")
with open("_bh_reopen3.txt", "w") as g3:
    g3.write("y")
with open("_bh_reopen3.txt") as r3:
    assert r3.read() == "y"

# double close is harmless; closed stays true
f4 = open("_bh_reopen3.txt")
f4.close()
f4.close()
assert f4.closed

# exception inside the with body with no writes still closes
try:
    with open("_bh_reopen4.txt", "w") as k:
        raise ValueError("b4")
except ValueError:
    pass
assert k.closed
with open("_bh_reopen4.txt", "w") as g4:
    g4.write("ok")
with open("_bh_reopen4.txt") as r4:
    assert r4.read() == "ok"

print("test_with_exception_file_reopen_regression passed")
