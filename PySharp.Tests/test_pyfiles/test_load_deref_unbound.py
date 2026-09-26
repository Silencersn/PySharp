"""
the by-name deref path (inline comprehensions per PEP 709 and
class bodies) must report an empty cell exactly like CPython and like the
_LoadDerefFast slot path already does: UnboundLocalError with the local
wording for a cellvar owned by the frame's code, NameError with the free
wording for a free variable (CPython _PyEval_FormatExcUnbound). The old
generic "cannot access local or free variable" wording does not exist in
CPython and the UnboundLocalError type was lost entirely.

:kind: test
"""

LOCAL_MSG = "cannot access local variable 'late' where it is not associated with a value"
FREE_MSG = "cannot access free variable 'late' where it is not associated with a value in enclosing scope"


def expect(tag, fn, exc_type, message):
    try:
        fn()
    except exc_type as e:
        assert str(e) == message, f"{tag}: {str(e)!r} != {message!r}"
    except Exception as e:
        raise AssertionError(f"{tag}: expected {exc_type.__name__}, got {type(e).__name__}: {e}")
    else:
        raise AssertionError(f"{tag}: no exception raised")


# case1: inline comprehension reads a not-yet-bound outer function local.
# PEP 709 inlining makes 'late' a cellvar of the function's own code, so
# CPython raises UnboundLocalError.
def case1():
    r = [late for _ in [1]]
    late = 1
    return r


expect("case1", case1, UnboundLocalError, LOCAL_MSG)


# case2: inline comprehension reads an outer local deleted before the call.
# The comprehension frame is gone by then, so the read goes through a real
# closure cell owned by inner's code: free variable, NameError.
def case2():
    def inner():
        return [late for _ in [1]]
    late = 1
    del late
    return inner()


expect("case2", case2, NameError, FREE_MSG)


# case3: the exception type is UnboundLocalError — `except UnboundLocalError`
# must catch it (the type divergence this file pins).
def case3():
    try:
        r = [late for _ in [1]]
    except UnboundLocalError as e:
        return str(e)
    finally:
        late = 1


assert case3() == LOCAL_MSG, case3()


# case4: a class body reading a deleted enclosing local goes through the
# by-name deref path too; 'late' is a freevar of the class code object.
def case4():
    def inner():
        class C:
            x = late
        return C.x
    late = 1
    del late
    return inner()


expect("case4", case4, NameError, FREE_MSG)


# case5: a genexp is a separate code object (its own closure cell) — the
# slot fast path; unchanged, pinned here so both paths stay aligned.
def case5():
    def inner():
        return list(late for _ in [1])
    late = 1
    del late
    return inner()


expect("case5", case5, NameError, FREE_MSG)


# Bound cells keep working on the by-name path (inline comprehension after
# the outer local is assigned; class body reading a live enclosing local).
def case_bound():
    late = 1
    r = [late for _ in [1]]
    return r


assert case_bound() == [1]


def case_bound_class():
    def inner():
        class C:
            x = late
        return C.x
    late = 42
    return inner()


assert case_bound_class() == 42

print("test_load_deref_unbound passed")
