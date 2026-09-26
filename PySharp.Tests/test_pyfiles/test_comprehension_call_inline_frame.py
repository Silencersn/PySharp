"""Verifies that function calls made inside an inlined comprehension frame do not detach or corrupt the enclosing frame's locals (no UnboundLocalError from pool-recycled spans), and that UnboundLocalError names the source variable instead of a slot index.

:kind: test
"""

# Regression: a function call made inside an inlined comprehension frame must
# not detach the enclosing frame's locals. When a call inside the
# comprehension returned, the VM rebound `locals` to the inline frame's copied
# span, and _ExitInlineFrame disposed that span back to the array pool — every
# later fast-local/deref access in the enclosing function then read zeroed,
# pool-recycled memory (UnboundLocalError with a slot-index name, or silent
# cross-frame corruption on writes).

# original repro: comp calls a closure capturing an outer local
def f_side_effects():
    calls = []
    def side(v):
        calls.append(v)
        return v
    r = [side(x) for x in [1, 0]]
    assert r == [1, 0]
    assert calls == [1, 0]
    return r, calls
assert f_side_effects() == ([1, 0], [1, 0])

# read-only capture through the same shape
def f_read_only():
    base = [1, 0]
    def side(v):
        return v + len(base)
    r = [side(x) for x in [1, 0]]
    assert r == [3, 2]
    assert base == [1, 0]
f_read_only()

# any call inside the comp (even a non-capturing one) while a cell exists
def f_noncapturing_call():
    calls = []
    def side(v):
        calls.append(v)
        return v
    g = lambda v: v * 2
    r = [g(x) for x in [1, 0]]
    assert r == [2, 0]
    assert calls == []
f_noncapturing_call()

# the closure keeps working across the comprehension boundary
def f_closure_alive():
    calls = []
    def side(v):
        calls.append(v)
        return v
    r = [side(x) for x in [1, 0]]
    side(9)
    assert calls == [1, 0, 9]
    assert r == [1, 0]
f_closure_alive()

# comprehension without calls is unaffected
def f_no_call():
    calls = []
    def side(v):
        calls.append(v)
        return v
    r = [x for x in [1, 0]]
    assert r == [1, 0]
    assert calls == []
f_no_call()

# multiple iterations after the call inside the comp body
def f_repeat_calls():
    seen = []
    def note(v):
        seen.append(v)
        return v
    r = [note(x) + note(x * 10) for x in range(3)]
    assert r == [0, 11, 22]
    assert seen == [0, 0, 1, 10, 2, 20]
f_repeat_calls()

# nested comprehensions with calls on both levels
def f_nested_comp():
    calls = []
    def note(v):
        calls.append(v)
        return v
    r = [[note(x) for x in range(2)] for y in range(2)]
    assert r == [[0, 1], [0, 1]]
    assert calls == [0, 1, 0, 1]
    return r, calls
assert f_nested_comp() == ([[0, 1], [0, 1]], [0, 1, 0, 1])

# genexp consumed inside a comp while a closure cell exists
def f_genexp_in_comp():
    calls = []
    def note(v):
        calls.append(v)
        return v
    r = [note(len(list(g))) for g in ((x for x in range(3)) for _ in range(2))]
    assert r == [3, 3]
    assert calls == [3, 3]
f_genexp_in_comp()

# calls in comprehension condition positions
def f_call_in_cond():
    calls = []
    def keep(v):
        calls.append(v)
        return v % 2 == 0
    r = [x for x in range(4) if keep(x)]
    assert r == [0, 2]
    assert calls == [0, 1, 2, 3]
f_call_in_cond()

# error messages name the source variable, not a slot index
def f_error_names():
    try:
        print(undefined_local)
    except UnboundLocalError as e:
        assert "local variable 'undefined_local'" in str(e)
    else:
        raise AssertionError("expected UnboundLocalError")
    undefined_local = 1  # the assignment makes the name local
f_error_names()

# same for a cell variable read before assignment
def f_error_cell_name():
    def inner():
        return late
    try:
        print(late)
    except UnboundLocalError as e:
        assert "local variable 'late'" in str(e)
    late = 1
f_error_cell_name()

print("test_comprehension_call_inline_frame passed")
