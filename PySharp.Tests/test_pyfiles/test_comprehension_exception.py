"""
Regression test: inline frame cleanup when exception occurs in comprehension
inside a function with try-except.

This test verifies that after a comprehension raises an exception that is
caught by an enclosing try-except in a function, the inline frame is properly
cleaned up and subsequent operations work correctly.

See: /memories/repo/inline-frame-exception-leak.md
"""

# ---------- Helper: track whether we reached end ----------
reached_end = False

# ========== Test 1: List comprehension exception in function ==========

def test_list_comp():
    local_flag = False
    try:
        result = [1 // 0 for x in range(3)]
        assert False, "Should have raised ZeroDivisionError"
    except ZeroDivisionError:
        local_flag = True

    assert local_flag, "Exception should have been caught"

    # Subsequent comprehension should work
    r2 = [x * 2 for x in range(5)]
    assert r2 == [0, 2, 4, 6, 8], f"Expected [0, 2, 4, 6, 8], got {r2}"

    # Subsequent function call should work
    def simple():
        return 42
    assert simple() == 42

    # Local variable access should work
    x = 100
    assert x == 100

test_list_comp()

# ========== Test 2: Set comprehension exception in function ==========

def test_set_comp():
    try:
        result = {1 // 0 for x in range(3)}
        assert False, "Should have raised ZeroDivisionError"
    except ZeroDivisionError:
        pass

    # Subsequent set comprehension should work
    r = {x % 3 for x in range(7)}
    assert 0 in r and 1 in r and 2 in r
    assert len(r) == 3

test_set_comp()

# ========== Test 3: Dict comprehension exception in function ==========

def test_dict_comp():
    try:
        result = {x: 1 // 0 for x in range(3)}
        assert False, "Should have raised ZeroDivisionError"
    except ZeroDivisionError:
        pass

    # Subsequent dict comprehension should work
    r = {x: x * 2 for x in range(4)}
    assert r[0] == 0 and r[1] == 2 and r[2] == 4 and r[3] == 6

test_dict_comp()

# ========== Test 4: Multiple comprehensions with exception in middle ==========

def test_multiple_comps():
    # First comprehension - should succeed
    r1 = [x * 10 for x in range(3)]
    assert r1 == [0, 10, 20]

    # Second comprehension - raises exception
    try:
        r2 = [1 // 0 for x in range(3)]
        assert False, "Should have raised ZeroDivisionError"
    except ZeroDivisionError:
        pass

    # Third comprehension - should still work
    r3 = [x * 100 for x in range(3)]
    assert r3 == [0, 100, 200]

    # Fourth - dict comprehension
    r4 = {x: x ** 2 for x in range(5)}
    assert r4[0] == 0 and r4[1] == 1 and r4[2] == 4 and r4[3] == 9 and r4[4] == 16

    # Fifth - set comprehension
    r5 = {x % 3 for x in range(7)}
    assert len(r5) == 3

test_multiple_comps()

# ========== Test 5: Exception with explicit return in except block ==========

def test_except_return():
    try:
        result = [1 // 0 for x in range(3)]
        assert False, "Should have raised ZeroDivisionError"
    except ZeroDivisionError:
        return "handled"
    # Should not reach here
    assert False, "Should have returned from except block"
    return "unreachable"

r = test_except_return()
assert r == "handled", f"Expected 'handled', got {r}"

# ========== Test 6: Nested functions with comprehension exception ==========

def test_nested():
    def inner():
        try:
            return [1 // 0 for x in range(2)]
        except ZeroDivisionError:
            return "inner_handled"
        return "unreachable"

    result = inner()
    assert result == "inner_handled", f"Expected 'inner_handled', got {result}"

    # After nested call, comprehensions should still work
    r = [x + 1 for x in range(4)]
    assert r == [1, 2, 3, 4]

test_nested()

# ========== Test 7: try-finally with comprehension exception ==========

def test_finally():
    finally_ran = [False]
    try:
        try:
            result = [1 // 0 for x in range(3)]
        finally:
            finally_ran[0] = True
    except ZeroDivisionError:
        assert finally_ran[0], "Finally should have run"
    else:
        assert False, "Should have raised ZeroDivisionError"

    # After finally, comprehensions should still work
    r = [x for x in range(5)]
    assert r == [0, 1, 2, 3, 4]

test_finally()

# ========== Test 8: Comprehension exception in for-loop body ==========

def test_for_loop():
    for i in range(3):
        if i == 1:
            try:
                result = [1 // 0 for x in range(2)]
                assert False, "Should have raised ZeroDivisionError"
            except ZeroDivisionError:
                pass
        else:
            r = [x for x in range(2)]
            assert r == [0, 1]

test_for_loop()

# ========== Test 9: Generator expression (should NOT create inline frame) ==========

def test_generator():
    # Generator expression is lazy - exception only on iteration
    gen = (1 // 0 for x in range(3))
    try:
        list(gen)
        assert False, "Should have raised ZeroDivisionError"
    except ZeroDivisionError:
        pass

    # Subsequent generator should work
    gen2 = (x * 2 for x in range(3))
    assert list(gen2) == [0, 2, 4]

test_generator()

# ========== All done ==========
reached_end = True
assert reached_end, "Should have reached the end"
