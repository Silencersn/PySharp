"""An exception carries its traceback chain and exposes it as __traceback__.

CPython attaches a traceback object as the exception travels (PyTraceBack_Here
from the interpreter error label), so an exception caught by except-as already
has a non-None __traceback__. The chain starts at the outermost frame and
tb_next walks towards the frame where the exception occurred — the order the
"Traceback (most recent call last)" display prints. The attribute is a writable
getset: None clears it, a traceback replaces it, anything else is a TypeError,
and deleting it is a TypeError. BaseException.with_traceback sets it and returns
self without touching the exception chain attributes.

:kind: test
:background: The attribute existed but its getter always returned None and
    writes were rejected, so the traceback module style of walking
    e.__traceback__ over the frame chain was unusable.
"""


def _depth(tb):
    n = 0
    while tb is not None:
        n += 1
        tb = tb.tb_next
    return n


def _linenos(tb):
    out = []
    while tb is not None:
        out.append(tb.tb_lineno)
        tb = tb.tb_next
    return out


def _raises(exc_type, fn):
    """Run fn and return the caught exception, or None if it did not raise."""
    try:
        fn()
    except exc_type as e:
        return e
    return None


def inner():
    raise ValueError("boom")


def outer():
    inner()


# --- a caught exception has a traceback chain ---------------------------------

try:
    outer()
except ValueError as e:
    tb = e.__traceback__
    assert tb is not None, "caught exception must carry a traceback"
    assert type(tb).__name__ == "traceback", type(tb).__name__
    assert type(tb).__module__ == "builtins", type(tb).__module__
    # reading does not rebuild the chain
    assert e.__traceback__ is tb, "reading __traceback__ must be stable"
    # the module frame called outer() which called inner(): three frames,
    # newest-to-oldest walking tb_next towards the raise site
    assert _depth(tb) == 3, _depth(tb)
    assert tb.tb_next is not None and tb.tb_next.tb_next is not None
    assert tb.tb_next.tb_next.tb_next is None, "inner() is the tail"
    assert type(tb.tb_next).__name__ == "traceback", type(tb.tb_next).__name__
    lines = _linenos(tb)
    assert all(isinstance(n, int) and not isinstance(n, bool) for n in lines)
    assert all(n > 0 for n in lines), lines
    assert len(set(lines)) == 3, lines
    # the tail is the raise statement: a second raise through the same
    # functions reports the same line, while the outer frames differ
    try:
        outer()
    except ValueError as again:
        again_lines = _linenos(again.__traceback__)
    assert again_lines[-1] == lines[-1], (again_lines, lines)
    # both attributes are listed on the object
    assert "tb_next" in dir(tb)
    assert "tb_lineno" in dir(tb)
    # default repr names the type, no __dict__
    assert repr(tb).startswith("<traceback object at 0x"), repr(tb)
    assert not hasattr(tb, "__dict__")

# a fresh (never raised) exception has no traceback
assert ValueError("fresh").__traceback__ is None

# a bare re-raise keeps the traceback it was caught with
try:
    try:
        inner()
    except ValueError:
        raise
except ValueError as rer:
    assert rer.__traceback__ is not None


# --- writing __traceback__ ----------------------------------------------------

try:
    inner()
except ValueError as e:
    tb = e.__traceback__
    e.__traceback__ = None
    assert e.__traceback__ is None, "None must clear the traceback"
    e.__traceback__ = tb
    assert e.__traceback__ is tb, "a traceback must be stored by identity"

    err = _raises(TypeError, lambda: setattr(e, "__traceback__", 42))
    assert err is not None, "an int must be rejected"
    assert str(err) == "__traceback__ must be a traceback or None", str(err)

    err = _raises(TypeError, lambda: setattr(e, "__traceback__", "x"))
    assert err is not None, "a str must be rejected"
    assert str(err) == "__traceback__ must be a traceback or None", str(err)

    err = _raises(TypeError, lambda: delattr(e, "__traceback__"))
    assert err is not None, "deleting __traceback__ must be rejected"
    assert str(err) == "__traceback__ may not be deleted", str(err)
    # the failed delete left the value alone
    assert e.__traceback__ is tb


# --- with_traceback -----------------------------------------------------------

try:
    inner()
except ValueError as e:
    tb = e.__traceback__
    assert e.with_traceback(tb) is e, "with_traceback must return self"
    assert e.with_traceback(None) is e
    assert e.__traceback__ is None, "with_traceback(None) must clear it"
    e.__traceback__ = tb

    err = _raises(TypeError, lambda: e.with_traceback(5))
    assert err is not None, "a non-traceback must be rejected"
    assert str(err) == "__traceback__ must be a traceback or None", str(err)

# with_traceback leaves cause/context/suppress_context untouched
try:
    try:
        raise KeyError("k")
    except KeyError as cause:
        raise ValueError("v") from cause
except ValueError as ex:
    context, cause, suppressed = ex.__context__, ex.__cause__, ex.__suppress_context__
    assert ex.with_traceback(ex.__traceback__) is ex
    assert ex.__context__ is context
    assert ex.__cause__ is cause
    assert ex.__suppress_context__ is suppressed


# --- tb_next ------------------------------------------------------------------

try:
    outer()
except ValueError as e:
    head = e.__traceback__
    tail = head.tb_next
    assert tail is not None

    head.tb_next = None
    assert head.tb_next is None, "None must clear tb_next"
    head.tb_next = tail
    assert head.tb_next is tail, "a traceback must be stored by identity"

    err = _raises(TypeError, lambda: setattr(head, "tb_next", 5))
    assert err is not None, "an int must be rejected as tb_next"
    assert str(err) == "expected traceback object, got 'int'", str(err)

    err = _raises(TypeError, lambda: delattr(head, "tb_next"))
    assert err is not None, "deleting tb_next must be rejected"
    assert str(err) == "can't delete tb_next attribute", str(err)

    # a chain that leads back to the node is rejected
    err = _raises(ValueError, lambda: setattr(head, "tb_next", head))
    assert err is not None, "a self-cycle must be rejected"
    assert str(err) == "traceback loop detected", str(err)

    err = _raises(ValueError, lambda: setattr(tail, "tb_next", head))
    assert err is not None, "an indirect cycle must be rejected"
    assert str(err) == "traceback loop detected", str(err)

# the original chain is intact after the rejected assignments
try:
    outer()
except ValueError as e:
    assert _depth(e.__traceback__) == 3, _depth(e.__traceback__)


# --- tb_lineno is read-only ---------------------------------------------------

try:
    inner()
except ValueError as e:
    tb = e.__traceback__
    err = _raises(AttributeError, lambda: setattr(tb, "tb_lineno", 5))
    assert err is not None, "tb_lineno must be read-only"
    assert str(err) == (
        "attribute 'tb_lineno' of 'traceback' objects is not writable"
    ), str(err)

    # deleting it reports the same read-only message
    err = _raises(AttributeError, lambda: delattr(tb, "tb_lineno"))
    assert err is not None, "tb_lineno must not be deletable"
    assert str(err) == (
        "attribute 'tb_lineno' of 'traceback' objects is not writable"
    ), str(err)


print("test_exception_traceback passed")
