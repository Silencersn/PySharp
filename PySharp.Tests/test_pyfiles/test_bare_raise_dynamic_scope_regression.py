# Regression test: bare `raise` dynamic-scope semantics (CPython exc_info).
#
# A bare raise re-raises the handled exception of the whole call chain, not
# just the current frame: helpers called from an except body, __exit__ during
# a with-body exception, and finally pass-through all observe the active
# exception; once a handler completes the state is restored, so a later bare
# raise with no active exception raises RuntimeError instead of a stale one.

def caught(fn):
    try:
        fn()
        return "no-exc"
    except BaseException as e:
        return f"{type(e).__name__}:{str(e)[:24]}"

# helper called from an except body: reraises the original
def helper():
    raise
def caller_except():
    try:
        raise ValueError("orig")
    except ValueError:
        return helper()
assert caught(caller_except) == "ValueError:orig"

# nested def inside an except body observes it too
def nested_in_except():
    try:
        raise KeyError("k")
    except KeyError:
        def inner():
            raise
        return inner()
assert caught(nested_in_except) == "KeyError:'k'"

# helper called from a finally while the exception passes through
def helper2():
    raise
def finally_pass():
    try:
        raise ValueError("orig")
    finally:
        helper2()
assert caught(finally_pass) == "ValueError:orig"

# with: __exit__ bare raise reraises the body exception; without one there
# is no active exception to reraise
class Ctx:
    def __enter__(self):
        return self
    def __exit__(self, *exc):
        raise
def exit_bare():
    with Ctx():
        raise ValueError("from-body")
assert caught(exit_bare) == "ValueError:from-body"

def exit_bare_clean():
    with Ctx():
        pass
assert caught(exit_bare_clean) == "RuntimeError:No active exception to r"

# no leak: after a handler completes the chain state is restored, so a bare
# raise afterwards sees no active exception again
def earlier():
    try:
        raise ValueError("v")
    finally:
        pass
assert caught(earlier) == "ValueError:v"
def later():
    raise
assert caught(later) == "RuntimeError:No active exception to r"

# an inner handler completing restores the outer one
def helper3():
    raise
def outer_active():
    try:
        raise KeyError("outer")
    except KeyError:
        try:
            raise ValueError("inner")
        except ValueError:
            pass
        return helper3()
assert caught(outer_active) == "KeyError:'outer'"

# nested handlers: the most recent one wins for callees
def helper4():
    raise
def latest_wins():
    try:
        raise TypeError("t1")
    except TypeError:
        try:
            raise ValueError("v2")
        except ValueError:
            return helper4()
assert caught(latest_wins) == "ValueError:v2"

# a generator frame observes the resuming caller's active exception
def gen():
    yield 1
    raise
def gen_caller():
    try:
        raise IndexError("idx")
    except IndexError:
        g = gen()
        next(g)
        return caught(lambda: next(g))
assert gen_caller() == "IndexError:idx"

print("test_bare_raise_dynamic_scope_regression passed")
