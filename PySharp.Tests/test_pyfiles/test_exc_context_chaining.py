"""Implicit __context__ chaining follows CPython's thread-wide handled slot: any new exception raised while another is handled chains it, including interpreter-raised and cross-frame errors, while reraise-style propagation never chains.

Covers except-clause matching errors, finally and with unwinds, generator throw/close injection and yield-from delegation, except* settlement, context-cycle breaking, and slot restoration after handlers complete.

:kind: test
"""
# Implicit __context__ chaining. CPython builds the chain when an exception
# is set (_PyErr_SetObject reads the thread-wide handled slot), so it also
# covers interpreter-raised errors, callee-frame raises and deferred error
# results; reraise-style propagation (bare raise, machinery rethrows) never
# chains, and an explicit re-raise overwrites the stale context.
def ctx_name(e):
    return type(e.__context__).__name__ if e.__context__ is not None else None

def caught(fn):
    try:
        fn()
    except BaseException as e:
        return e
    raise AssertionError("expected an exception")

# --- except-clause matching errors carry the handled exception ---

def a_nested_tuple():
    try:
        raise KeyError("k")
    except (ValueError, (KeyError, IndexError)):
        pass

e = caught(a_nested_tuple)
assert type(e).__name__ == "TypeError"
assert ctx_name(e) == "KeyError", ctx_name(e)

def b_nonclass_item():
    try:
        raise KeyError("k")
    except (ValueError, 42):
        pass

e = caught(b_nonclass_item)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "KeyError"

def c_nonclass_expr():
    try:
        raise KeyError("k")
    except 42:
        pass

e = caught(c_nonclass_expr)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "KeyError"

# --- interpreter-raised errors chain too ---

def m_nameerror_in_expr():
    try:
        raise KeyError("k")
    except undefined_name:
        pass

e = caught(m_nameerror_in_expr)
assert type(e).__name__ == "NameError" and ctx_name(e) == "KeyError"

def o_zerodiv_in_callee_expr():
    def divider():
        return 1 // 0
    try:
        raise KeyError("k")
    except (ValueError, divider()):
        pass

e = caught(o_zerodiv_in_callee_expr)
assert type(e).__name__ == "ZeroDivisionError" and ctx_name(e) == "KeyError"

# --- cross-frame raises chain ---

def raiser():
    raise TypeError("callee")

def n_callee_raise_in_expr():
    try:
        raise KeyError("k")
    except (ValueError, raiser()):
        pass

e = caught(n_callee_raise_in_expr)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "KeyError"

def log_error():
    raise TypeError("log exploded")

def s_callee_raise_in_body():
    try:
        int("abc")
    except ValueError:
        log_error()

e = caught(s_callee_raise_in_body)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "ValueError"

# --- in-frame raise faces keep chaining ---

def t_inframe_raise_in_body():
    try:
        int("abc")
    except ValueError:
        raise TypeError("in-body")

e = caught(t_inframe_raise_in_body)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "ValueError"

def p4_raise_in_finally():
    try:
        raise KeyError("k")
    finally:
        raise TypeError("t")

e = caught(p4_raise_in_finally)
assert ctx_name(e) == "KeyError"

def z_with_exit_raise():
    class CM:
        def __enter__(self):
            return self
        def __exit__(self, *args):
            raise TypeError("exit")
    try:
        raise KeyError("k")
    except KeyError:
        with CM():
            pass

e = caught(z_with_exit_raise)
assert ctx_name(e) == "KeyError"

def x_comprehension_zerodiv():
    try:
        raise KeyError("k")
    except KeyError:
        return [1 // 0 for _ in range(1)]

e = caught(x_comprehension_zerodiv)
assert type(e).__name__ == "ZeroDivisionError" and ctx_name(e) == "KeyError"

def gen_raise():
    raise TypeError("g")
    yield 1

def y_generator_raise():
    try:
        raise KeyError("k")
    except KeyError:
        list(gen_raise())

e = caught(y_generator_raise)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "KeyError"

# --- chaining semantics ---

# explicit re-raise of a stale exception overwrites the context
def p2_stale_reraise():
    e2 = None
    try:
        try:
            raise ValueError("v1")
        except ValueError:
            raise TypeError("t1")
    except TypeError as te:
        e2 = te
    assert ctx_name(e2) == "ValueError"
    try:
        raise KeyError("k")
    except KeyError:
        raise e2

e = caught(p2_stale_reraise)
assert ctx_name(e) == "KeyError", ctx_name(e)

# bare re-raise keeps the existing context
def p7_bare_reraise():
    try:
        try:
            try:
                raise ValueError("v1")
            except ValueError:
                raise TypeError("t1")
        except TypeError:
            raise
    except TypeError as inner:
        return inner

e = p7_bare_reraise()
assert ctx_name(e) == "ValueError"

# raise-from sets the cause, suppresses the context in display, and still
# records the handled exception as __context__
def p3_raise_from():
    try:
        try:
            raise KeyError("k")
        except KeyError:
            raise TypeError("t") from ValueError("cause")
    except TypeError as inner:
        return inner

e = p3_raise_from()
assert ctx_name(e) == "KeyError"
assert type(e.__cause__).__name__ == "ValueError"
assert e.__suppress_context__ is True

# nested handler adoption chains the inner raise to the outer handled one
def ad_nested_adoption():
    try:
        raise KeyError("k")
    except KeyError:
        try:
            raise ValueError("v")
        except ValueError as inner:
            return inner

e = ad_nested_adoption()
assert ctx_name(e) == "KeyError"

# deferred error results chain when they are finally raised
def aa_dictmiss_deferred():
    try:
        raise KeyError("outer")
    except KeyError:
        try:
            {}["x"]
        except KeyError as inner:
            return inner

e = aa_dictmiss_deferred()
assert ctx_name(e) == "KeyError", ctx_name(e)

def ac_int_valueerror_deferred():
    try:
        raise KeyError("outer")
    except KeyError:
        try:
            int("zz")
        except ValueError as inner:
            return inner

e = ac_int_valueerror_deferred()
assert ctx_name(e) == "KeyError"

# --- except* settlement keeps reraise metadata clean ---

def f2_bare_reraise_recombine():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2)])
    except* ValueError:
        raise

e = caught(f2_bare_reraise_recombine)
assert type(e).__name__ == "ExceptionGroup" and str(e) == "m (2 sub-exceptions)"
assert [type(s).__name__ for s in e.exceptions] == ["ValueError", "TypeError"]
assert e.__context__ is None

def f3_new_raise_settlement():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2), KeyError(3)])
    except* ValueError as eg:
        raise KeyError("new")

e = caught(f3_new_raise_settlement)
assert type(e).__name__ == "ExceptionGroup"
assert e.__context__ is None

def p1_subgroup_is_chain_source():
    try:
        raise ExceptionGroup("g", [TypeError("t"), ValueError("v")])
    except* TypeError:
        raise KeyError("k")

e = caught(p1_subgroup_is_chain_source)
inner = e.exceptions[0]
assert type(inner).__name__ == "KeyError"
assert isinstance(inner.__context__, ExceptionGroup)
assert [type(s).__name__ for s in inner.__context__.exceptions] == ["TypeError"]

# --- cycle safety and remaining set paths ---

# re-raising an ancestor exception must not close a context cycle: the
# traceback printer recurses along __context__, and CPython breaks the stale
# link before overwriting (Floyd walk over the handled chain)
def cyc_break_same_frame():
    try:
        try:
            raise ValueError("inner")
        except ValueError:
            raise TypeError("outer")
    except TypeError as outer:
        raise outer.__context__

e = caught(cyc_break_same_frame)
assert type(e).__name__ == "ValueError" and ctx_name(e) == "TypeError"

def cyc_break_cross_frame():
    def reraise(x):
        raise x
    try:
        try:
            raise ValueError("inner")
        except ValueError:
            raise TypeError("outer")
    except TypeError as outer:
        reraise(outer.__context__)

e = caught(cyc_break_cross_frame)
assert type(e).__name__ == "ValueError" and ctx_name(e) == "TypeError"

# explicit re-raise of the exception being handled skips chaining entirely
def same_object_reraise():
    try:
        raise ValueError("v")
    except ValueError as e:
        raise e

e = caught(same_object_reraise)
assert type(e).__name__ == "ValueError" and ctx_name(e) is None

# native comparison throw inside sort chains like any deferred error result
def sort_comparison_error():
    try:
        raise KeyError("k")
    except KeyError:
        sorted([1, "a"])

e = caught(sort_comparison_error)
assert type(e).__name__ == "TypeError" and ctx_name(e) == "KeyError"

# a generator resumed while suspended inside its own handler chains to its
# own handled exception, not to the caller's (or to none)
def gen_suspended_in_own_except():
    try:
        yield 1
        raise KeyError("k")
    except KeyError:
        yield 2
        {}["x"]
        yield 3

g = gen_suspended_in_own_except()
next(g)
next(g)
e = caught(lambda: next(g))
assert type(e).__name__ == "KeyError" and ctx_name(e) == "KeyError", ctx_name(e)

g = gen_suspended_in_own_except()
next(g)
next(g)
def resume_in_active_handler():
    try:
        raise ValueError("v")
    except ValueError:
        next(g)

e = caught(resume_in_active_handler)
assert type(e).__name__ == "KeyError" and ctx_name(e) == "KeyError", ctx_name(e)

# raise-from-None keeps the implicit context recorded but suppressed
def raise_from_none():
    try:
        raise KeyError("k")
    except KeyError:
        raise TypeError("t") from None

e = caught(raise_from_none)
assert e.__cause__ is None
assert e.__suppress_context__ is True
assert ctx_name(e) == "KeyError"

# --- generator throw-in / close injection settles the context once ---

# CPython _PyErr_ChainStackItem: the generator's own handled slot chains
# onto the injected exception with overwrite semantics, never falling back
# to the caller's slot; the context is not re-chained on later propagation
def gen_for_injection():
    try:
        yield 1
        raise KeyError("k")
    except KeyError:
        yield 2
        yield 3

g = gen_for_injection()
next(g)
next(g)
e = caught(lambda: g.throw(ValueError("v")))
assert type(e).__name__ == "ValueError" and ctx_name(e) == "KeyError", ctx_name(e)

g = gen_for_injection()
next(g)
next(g)
stale = TypeError("old")
stale.__context__ = ValueError("stale-ctx")
e = caught(lambda: g.throw(stale))
assert type(e).__name__ == "TypeError" and ctx_name(e) == "KeyError", ctx_name(e)

close_captured = None
def gen_for_close():
    try:
        yield 1
        raise KeyError("k")
    except KeyError:
        try:
            yield 2
        except BaseException as ge:
            global close_captured
            close_captured = type(ge.__context__).__name__ if ge.__context__ is not None else None
            raise

g = gen_for_close()
next(g)
next(g)
g.close()
assert close_captured == "KeyError", close_captured
# injection outside any handler of the generator must not chain the
# caller's active exception either
g = gen_for_injection()
next(g)
def throw_in_without_handler():
    try:
        raise ValueError("caller-v")
    except ValueError:
        g.throw(TypeError("injected"))

e = caught(throw_in_without_handler)
assert type(e).__name__ == "TypeError" and ctx_name(e) is None, ctx_name(e)

# --- dead-generator throw and yield from delegation re-injection ---

# CPython gen_throw exits before swapping the exception state, so a dead
# generator's throw propagates without chaining anything
def dead_gen_throw():
    def gen():
        yield 1
    g = gen()
    next(g)
    caught(lambda: next(g))
    try:
        raise KeyError("caller-k")
    except KeyError:
        g.throw(ValueError("v"))

e = caught(dead_gen_throw)
assert type(e).__name__ == "ValueError" and ctx_name(e) is None, ctx_name(e)

def dead_gen_throw_fresh():
    def gen():
        yield 1
    g = gen()
    try:
        raise KeyError("caller-k")
    except KeyError:
        g.throw(ValueError("v"))

e = caught(dead_gen_throw_fresh)
assert type(e).__name__ == "ValueError" and ctx_name(e) is None, ctx_name(e)

# a failed sub-throw across yield from is re-injected into the outer frame:
# CPython chains the outer generator's own handled slot (overwrite)
def deleg_throw_in_outer_handler():
    def sub():
        yield 1
        yield 2
    def outer():
        try:
            raise KeyError("outer-k")
        except KeyError:
            yield from sub()
    g = outer()
    next(g)
    e = caught(lambda: g.throw(TypeError("injected")))
    return e

e = deleg_throw_in_outer_handler()
assert type(e).__name__ == "TypeError"
assert type(e.__context__).__name__ == "KeyError", ctx_name(e)
assert e.__context__.args == ("outer-k",), e.__context__.args

# same shape with nested delegation (two levels)
def deleg_throw_nested():
    def leaf():
        yield 1
    def mid():
        yield from leaf()
    def outer():
        try:
            raise KeyError("outer-k")
        except KeyError:
            yield from mid()
    g = outer()
    next(g)
    e = caught(lambda: g.throw(TypeError("injected")))
    return e

e = deleg_throw_nested()
assert type(e).__name__ == "TypeError"
assert e.__context__.args == ("outer-k",), e.__context__.args

# --- machinery-created errors and delegation close failures ---

# "generator ignored GeneratorExit" is created inside gen_close with
# PyErr_SetString, so it chains the handled slot the close() call observes
def s_ignored_exit():
    def sub():
        try:
            yield 1
        except GeneratorExit:
            yield 2
    def outer():
        yield from sub()
    g = outer()
    next(g)
    try:
        raise ValueError("caller-v")
    except ValueError:
        g.close()

e = caught(s_ignored_exit)
assert type(e).__name__ == "RuntimeError" and ctx_name(e) == "ValueError", ctx_name(e)

# PEP 479: the RuntimeError replacing an escaping StopIteration carries the
# StopIteration as both __cause__ and __context__
def v_pep479_active_handler():
    def gen():
        raise StopIteration
        yield 1
    g = gen()
    try:
        raise ValueError("caller-v")
    except ValueError:
        next(g)

e = caught(v_pep479_active_handler)
assert type(e).__name__ == "RuntimeError"
assert isinstance(e.__cause__, StopIteration)
assert type(e.__context__).__name__ == "StopIteration", ctx_name(e)

def v2_pep479_no_handler():
    def gen():
        raise StopIteration
        yield 1
    g = gen()
    next(g)

e = caught(v2_pep479_no_handler)
assert type(e).__name__ == "RuntimeError"
assert isinstance(e.__cause__, StopIteration)
assert type(e.__context__).__name__ == "StopIteration", ctx_name(e)

# a failed sub-close consumes this frame's GeneratorExit (CPython gen_close
# never delivers it on that path), so the generator is not bricked
def m_next_after_failed_close():
    def sub():
        try:
            yield 1
        except GeneratorExit:
            raise ValueError("sub-close-fail")
    def outer():
        try:
            yield from sub()
        except ValueError:
            yield "done"
    g = outer()
    next(g)
    e = caught(lambda: g.close())
    v = caught(lambda: next(g))
    return e, v

e, v = m_next_after_failed_close()
assert type(e).__name__ == "RuntimeError", type(e).__name__
assert type(v).__name__ == "StopIteration", type(v).__name__

# --- round-5 faces: suppress flag, GE chain source, close-lookup failure ---

# PEP 479 RuntimeError suppresses the implicit context (PyException_SetCause)
def t1_pep479_suppress():
    def gen():
        raise StopIteration
        yield 1
    g = gen()
    try:
        raise ValueError("v")
    except ValueError:
        next(g)

e = caught(t1_pep479_suppress)
assert type(e).__name__ == "RuntimeError"
assert isinstance(e.__cause__, StopIteration)
assert type(e.__context__).__name__ == "StopIteration"
assert e.__suppress_context__ is True

# close-injected GeneratorExit chains the close caller's handled slot when
# the generator has no handler of its own (gen_close raises it before
# swapping the exception state)
n2_captured = None
def n2_gen():
    try:
        yield 1
    except GeneratorExit as ge:
        global n2_captured
        n2_captured = ctx_name(ge)
        raise

def n2_driver():
    g = n2_gen()
    next(g)
    try:
        raise ValueError("caller-v")
    except ValueError:
        g.close()

n2_driver()
assert n2_captured == "ValueError", n2_captured

# a failed close-lookup must not brick the generator with the stale
# GeneratorExit: the next resume ends in StopIteration (CPython swallows the
# lookup error as unraisable and closes cleanly - that display difference is
# out of scope here; the no-brick outcome is the shared contract)
def n1b_no_brick():
    class Weird:
        def __iter__(self):
            return self
        def __next__(self):
            return 1
        @property
        def close(self):
            raise RuntimeError("boom")
    def outer():
        yield from Weird()
    g = outer()
    next(g)
    try:
        g.close()
    except BaseException:
        # PySharp surfaces the failed sub-close lookup; CPython swallows it
        # as unraisable - the shared contract is only that the generator
        # stays resumable afterwards
        pass
    return type(caught(lambda: next(g)))

assert n1b_no_brick().__name__ == "StopIteration"

# --- round-6 faces: delegation region observes the close caller's slot ---

# gen_close/gen_throw run while the delegating frame is suspended, so the
# close-injected GeneratorExit chains the CLOSE caller's handled slot even
# when the delegating generator is suspended inside a handler of its own
gc_holder = []
def gc_sub():
    try:
        yield 1
    except GeneratorExit as ge:
        gc_holder.append(ctx_name(ge))
        raise

def gc_deleg():
    try:
        raise ValueError("gv")
    except ValueError:
        yield from gc_sub()

it = gc_deleg()
next(it)
try:
    raise KeyError("close-caller")
except KeyError:
    it.close()
assert gc_holder == ["KeyError"], gc_holder

# same shape through the throw() delegation path (gen_close_iter via
# _gen_throw's close_on_genexit)
gt_holder = []
def gt_sub():
    try:
        yield 1
    except GeneratorExit as ge:
        gt_holder.append(ctx_name(ge))
        raise

def gt_deleg():
    try:
        raise ValueError("gv")
    except ValueError:
        yield from gt_sub()

it = gt_deleg()
next(it)
try:
    raise KeyError("throw-caller")
except KeyError:
    try:
        it.throw(GeneratorExit())
    except (StopIteration, GeneratorExit):
        pass
assert gt_holder == ["KeyError"], gt_holder

# --- round-7 face: interpreter errors inside an except* body chain the
# MATCHED subgroup (CHECK_EG_MATCH swaps the handled slot to the subgroup
# via PyErr_SetHandledException), not the original group
def eg_subgroup_ctx_source():
    captured = []
    def f():
        try:
            raise ExceptionGroup("g", [ValueError("v"), TypeError("t")])
        except* ValueError:
            try:
                1 // 0
            except ZeroDivisionError as z:
                captured.append(None if z.__context__ is None
                                else len(z.__context__.exceptions))
    caught(f)   # the unmatched TypeError rest propagates from the statement
    return captured

assert eg_subgroup_ctx_source() == [1], eg_subgroup_ctx_source()

# --- round-8 faces: settlement re-raise publishes/restores the slot ---

# errors inside a finally entered by the settlement re-raise chain the
# propagating settlement group (CPython: the unwind region is wrapped in
# PUSH_EXC_INFO before the finally body runs)
def eg_finally_ctx():
    try:
        raise ExceptionGroup("g", [ValueError(1), TypeError(2)])
    except* ValueError:
        pass
    finally:
        1 // 0

e = caught(eg_finally_ctx)
assert type(e).__name__ == "ZeroDivisionError", type(e).__name__
assert [type(s).__name__ for s in e.__context__.exceptions] == ["TypeError"]

# a fully-consumed settlement leaves no handled-slot residue behind
try:
    try:
        raise ExceptionGroup("g", [ValueError(1), TypeError(2)])
    except* ValueError:
        pass
except* TypeError:
    pass

e = caught(lambda: 1 // 0)
assert ctx_name(e) is None, ctx_name(e)

# the list-repeat index overflow is a fresh interpreter error raised inside
# the built-in body, so it chains the handled slot like any other
def list_mul_ctx():
    try:
        raise ValueError("v")
    except ValueError:
        [] * (2 ** 100)

e = caught(list_mul_ctx)
assert type(e).__name__ == "OverflowError", type(e).__name__
assert ctx_name(e) == "ValueError", ctx_name(e)

# --- round-9 faces: finally rethrow unwinds the frame exception stack ---

# after a try-finally escape was caught, a bare raise in the same frame has
# no active exception (CPython POP_EXCEPT unwound the exception stack)
def dead_reraise():
    try:
        try:
            raise ValueError("a")
        finally:
            pass
    except ValueError:
        pass
    raise

e = caught(dead_reraise)
assert type(e).__name__ == "RuntimeError", type(e).__name__

# ... and the next fresh raise in that frame chains nothing stale
def stale_chain_source():
    try:
        try:
            raise ValueError("a")
        finally:
            pass
    except ValueError:
        pass
    raise TypeError("t")

e = caught(stale_chain_source)
assert type(e).__name__ == "TypeError" and ctx_name(e) is None, ctx_name(e)

# manually-built __context__ cycles are CPython-legal and must render
# without recursing forever (traceback._seen)
def manual_cycle():
    a = ValueError("a")
    b = TypeError("b")
    a.__context__ = b
    b.__context__ = a
    raise a

e = caught(manual_cycle)
assert type(e).__name__ == "ValueError" and type(e.__context__).__name__ == "TypeError"

# pop/insert index overflow chains like any built-in body error
def pop_overflow_ctx():
    try:
        raise ValueError("v")
    except ValueError:
        [1].pop(2 ** 100)

e = caught(pop_overflow_ctx)
assert type(e).__name__ == "OverflowError" and ctx_name(e) == "ValueError"

def insert_overflow_ctx():
    try:
        raise ValueError("v")
    except ValueError:
        [].insert(2 ** 100, 1)

e = caught(insert_overflow_ctx)
assert type(e).__name__ == "OverflowError" and ctx_name(e) == "ValueError"

# the empty-list pop's index conversion happens first, so the huge index
# overflows before the pop-from-empty message either way
def pop_empty_message():
    try:
        raise ValueError("v")
    except ValueError:
        [].pop(2 ** 100)

e = caught(pop_empty_message)
assert type(e).__name__ == "OverflowError" and ctx_name(e) == "ValueError"

# --- round-10 face: an exhausted rest reaches later handlers as None
# (CPython _PyEval_ExceptionGroupMatch short-circuits rest = match = None)

def eg_multi_handler_full_match():
    try:
        raise ExceptionGroup("g", [TypeError("t")])
    except* TypeError:
        pass
    except* ValueError:
        pass

eg_multi_handler_full_match()

def eg_multi_handler_bare():
    try:
        raise TypeError("t")
    except* TypeError:
        pass
    except* ValueError:
        pass

eg_multi_handler_bare()

def eg_multi_handler_partial():
    try:
        raise ExceptionGroup("g", [TypeError("t"), ValueError("v")])
    except* TypeError:
        pass
    except* KeyError:
        pass

e = caught(eg_multi_handler_partial)
assert type(e).__name__ == "ExceptionGroup"
assert [type(s).__name__ for s in e.exceptions] == ["ValueError"]

# --- round-11 faces: settlement slot survives suspends; fast-path still
# validates the match type ---

# the settlement-published handled slot survives a generator suspend in the
# statement's finally (CPython gi_exc_state carries exc_value across suspends)
def eg_settle_suspend_ctx():
    def gen():
        try:
            raise ExceptionGroup("g", [ValueError(1)])
        except* ValueError:
            raise TypeError("in handler")
        finally:
            yield 1
            undefined_name_xyz
    def drive():
        try:
            raise ExceptionGroup("outer", [KeyError("k")])
        except* KeyError:
            for _ in gen():
                pass
    e = caught(drive)
    return type(e).__name__, repr(e.__context__)

name, ctx_repr = eg_settle_suspend_ctx()
assert name == "NameError" and ctx_repr == "TypeError('in handler')", (name, ctx_repr)

# an injection into that suspended generator chains the settlement
# exception (_PyErr_ChainStackItem reads gi_exc_state.exc_value)
def eg_settle_suspend_inject():
    captured = []
    def gen():
        try:
            raise ExceptionGroup("g", [ValueError(1)])
        except* ValueError:
            raise TypeError("in handler")
        finally:
            try:
                yield 1
            except BaseException as exc:
                captured.append(ctx_name(exc))
                raise
    g = gen()
    try:
        raise ExceptionGroup("outer", [KeyError("k")])
    except* KeyError:
        next(g)
        try:
            g.throw(ValueError("inj"))
        except (StopIteration, GeneratorExit, ValueError):
            pass
    return captured

assert eg_settle_suspend_inject() == ["TypeError"], eg_settle_suspend_inject()

# the exhausted-rest fast path still validates later handlers' match types
def eg_fastpath_invalid_type():
    try:
        raise ExceptionGroup("g", [ValueError(1)])
    except* ValueError:
        pass
    except* 123:
        pass

e = caught(eg_fastpath_invalid_type)
assert type(e).__name__ == "TypeError", type(e).__name__
assert "do not inherit from BaseException" in str(e), str(e)

# --- round-12 faces: a finally-body raise replaces the unwind state
# cleanly — nothing of the replaced exception may leak into later raises ---

def finally_raise_replace_ctx():
    try:
        try:
            raise ValueError("v")
        finally:
            raise KeyError("k")
    except KeyError:
        pass
    raise TypeError("t")

e = caught(finally_raise_replace_ctx)
assert type(e).__name__ == "TypeError" and ctx_name(e) is None, ctx_name(e)

def finally_raise_replace_bare():
    try:
        try:
            raise ValueError("v")
        finally:
            raise KeyError("k")
    except KeyError:
        pass
    raise

e = caught(finally_raise_replace_bare)
assert type(e).__name__ == "RuntimeError", type(e).__name__

# the finally-body raise itself chains the in-flight exception
def finally_raise_chain():
    result = []
    try:
        try:
            raise ValueError("v")
        finally:
            raise KeyError("k")
    except KeyError as ke:
        result.append((type(ke).__name__, ctx_name(ke)))
    return result

assert finally_raise_chain() == [("KeyError", "ValueError")], finally_raise_chain()

# --- round-13 faces: normally-entered finally and abandoned unwinds ---

# a raise inside a normally-entered finally must not wipe the ambient
# handled slot for later raises in the frame
def normal_finally_raise_ctx():
    late = None
    try:
        raise ValueError("E0")
    except ValueError:
        try:
            try:
                pass
            finally:
                1 // 0
        except ZeroDivisionError:
            pass
        try:
            raise KeyError("late")
        except KeyError as ke:
            late = ke
    return ctx_name(late)

assert normal_finally_raise_ctx() == "ValueError", normal_finally_raise_ctx()

# a with-statement __exit__ raising on normal exit behaves the same way
def with_exit_raise_ctx():
    late = None
    class CM:
        def __enter__(self):
            return self
        def __exit__(self, *args):
            raise KeyError("exit")
    try:
        raise ValueError("E0")
    except ValueError:
        try:
            with CM():
                pass
        except KeyError:
            pass
        try:
            raise TypeError("late")
        except TypeError as te:
            late = te
    return ctx_name(late)

assert with_exit_raise_ctx() == "ValueError", with_exit_raise_ctx()

# a break abandoning an unwind discards the in-flight exception entirely
def break_in_finally_ctx():
    for _ in range(2):
        try:
            raise ValueError("v1")
        finally:
            break
    raise TypeError("t")

e = caught(break_in_finally_ctx)
assert type(e).__name__ == "TypeError" and ctx_name(e) is None, ctx_name(e)

# continue across iterations must not chain the previous dropped exception
def continue_in_finally_ctx():
    seen = []
    for i in range(2):
        try:
            if i == 0:
                raise ValueError("v1")
        finally:
            continue
    try:
        raise TypeError("t")
    except TypeError as te:
        seen.append(ctx_name(te))
    return seen

assert continue_in_finally_ctx() == [None], continue_in_finally_ctx()

# --- round-14 faces: __exit__ raising ---

# with body raises and __exit__ raises: the exit error chains the body's
# exception (the with unwind keeps the in-flight exception on the frame
# stack and as the handled slot while __exit__ runs)
def with_body_and_exit_raise():
    class CM:
        def __enter__(self):
            return self
        def __exit__(self, *args):
            raise RuntimeError("from-exit")
    try:
        with CM():
            raise ValueError("orig")
    except RuntimeError as exit_error:
        return type(exit_error).__name__, ctx_name(exit_error)

assert with_body_and_exit_raise() == ("RuntimeError", "ValueError")

# no exception in the body: the exit error has nothing to chain
def with_exit_raise_no_body_error():
    class CM:
        def __enter__(self):
            return self
        def __exit__(self, *args):
            raise RuntimeError("from-exit")
    try:
        with CM():
            pass
    except RuntimeError as exit_error:
        return ctx_name(exit_error)

assert with_exit_raise_no_body_error() is None

# nested with: the inner exit error is in flight when the outer __exit__
# raises, so the outer error chains the inner one
def nested_with_exit_chain():
    class Outer:
        def __enter__(self):
            return self
        def __exit__(self, *args):
            raise RuntimeError("outer-exit")
    class Inner:
        def __enter__(self):
            return self
        def __exit__(self, *args):
            raise RuntimeError("inner-exit")
    try:
        with Outer():
            with Inner():
                raise ValueError("orig")
    except RuntimeError as exit_error:
        return type(exit_error).__name__, ctx_name(exit_error)

assert nested_with_exit_chain() == ("RuntimeError", "RuntimeError")
