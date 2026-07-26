"""
Regression test: try-except-else semantics.

Tests that:
1. Exceptions raised in the else block are NOT caught by the except clause
   of the same try statement (CPython behavior).
2. Normal else execution when no exception occurs.
3. try-except-else-finally interaction.
"""

# Test 1: exception in else propagates (NOT caught by same try's except)
caught_by_inner = False
caught_by_outer = False
try:
    try:
        pass
    except TypeError:
        caught_by_inner = True
    else:
        raise TypeError
    assert False, "TypeError from else should have propagated"
except TypeError:
    caught_by_outer = True
assert not caught_by_inner, "except should NOT catch TypeError from else"
assert caught_by_outer, "TypeError from else should propagate to outer try"

# Test 2: normal else execution (no exception in try body)
else_executed = False
try:
    x = 1 + 1
except:
    assert False, "Exception should not be raised"
else:
    else_executed = True
    assert x == 2
assert else_executed, "else block should execute when no exception"

# Test 3: try-except-else-finally (normal flow)
finally_executed = False
else_executed = False
try:
    x = 42
except RuntimeError:
    assert False, "Should not catch RuntimeError"
else:
    else_executed = True
finally:
    finally_executed = True
assert else_executed, "else block should execute"
assert finally_executed, "finally block should execute"

# Test 4: exception in else with finally - finally runs, except does NOT
finally_executed = False
try:
    try:
        pass
    except TypeError:
        assert False, "except should NOT catch TypeError from else"
    else:
        raise TypeError
    finally:
        finally_executed = True
except TypeError:
    pass  # Catch the propagated TypeError
assert finally_executed, "finally should execute even when else raises"

print("test_try_except_else passed")

