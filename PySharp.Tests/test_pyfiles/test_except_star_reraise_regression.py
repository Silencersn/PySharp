# except* (PEP 654) unwinding: a bare raise inside a handler re-raises the
# matched subgroup (recombined with the unmatched rest when one exists),
# a new exception escaping the handler must not drop the rest, and a bare
# raise with no live exception is a catchable RuntimeError.
def probe(fn):
    try:
        fn()
        raise AssertionError("expected an exception")
    except BaseException as e:
        return e

def f1():
    try:
        raise ExceptionGroup("m", [ValueError(1)])
    except* ValueError:
        raise

e = probe(f1)
assert type(e).__name__ == "ExceptionGroup" and str(e) == "m (1 sub-exception)"
assert [type(s).__name__ for s in e.exceptions] == ["ValueError"]

def f2():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2)])
    except* ValueError:
        raise

e = probe(f2)
assert str(e) == "m (2 sub-exceptions)"
assert [type(s).__name__ for s in e.exceptions] == ["ValueError", "TypeError"]

def f3():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2), KeyError(3)])
    except* ValueError as e:
        raise KeyError("new")

e = probe(f3)
assert str(e) == " (2 sub-exceptions)"
subs = list(e.exceptions)
assert [type(s).__name__ for s in subs] == ["KeyError", "ExceptionGroup"]
assert [type(s).__name__ for s in subs[1].exceptions] == ["TypeError", "KeyError"]

def f4():
    try:
        raise ExceptionGroup("m", [ValueError(1)])
    except* ValueError as e:
        raise KeyError("new")

e = probe(f4)
assert type(e).__name__ == "KeyError" and str(e) == str(KeyError("new"))

def f6():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2)])
    except* ValueError as e:
        raise e.exceptions[0]

e = probe(f6)
assert [type(s).__name__ for s in e.exceptions] == ["ValueError", "ExceptionGroup"]

def f7():
    try:
        raise ValueError("plain")
    except* TypeError:
        pass

e = probe(f7)
assert type(e).__name__ == "ValueError" and str(e) == "plain"

def f8():
    try:
        raise ValueError("plain")
    except* ValueError:
        raise

e = probe(f8)
assert type(e).__name__ == "ExceptionGroup"
assert [type(s).__name__ for s in e.exceptions] == ["ValueError"]

def f9():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2)])
    except* ValueError:
        pass

e = probe(f9)
assert str(e) == "m (1 sub-exception)"
assert [type(s).__name__ for s in e.exceptions] == ["TypeError"]

def f10():
    try:
        raise ExceptionGroup("m", [ValueError(1), TypeError(2), KeyError(3)])
    except* (ValueError, KeyError) as e:
        pass
    except* TypeError as e:
        raise RuntimeError("boom")

e = probe(f10)
assert type(e).__name__ == "RuntimeError" and str(e) == "boom"

def f11():
    try:
        raise ExceptionGroup("m", [ValueError(1)])
    except* ValueError as e:
        try:
            raise KeyError("inner")
        except* KeyError:
            raise

e = probe(f11)
assert [type(s).__name__ for s in e.exceptions] == ["KeyError"]

def f2c():
    try:
        raise ExceptionGroup("G", [ExceptionGroup("H", [ValueError(1)]), TypeError(2)])
    except* ValueError:
        raise

e = probe(f2c)
assert str(e) == "G (2 sub-exceptions)"
assert [type(s).__name__ for s in e.exceptions] == ["ExceptionGroup", "TypeError"]

def bare():
    raise

e = probe(bare)
assert type(e).__name__ == "RuntimeError" and str(e) == "No active exception to reraise"

g = ExceptionGroup("msg", [ValueError(1), TypeError(2)])
assert g.message == "msg"
assert [type(s).__name__ for s in g.exceptions] == ["ValueError", "TypeError"]

# a raise in one handler does not skip later handlers: they still run
# against the remaining rest, and all raises accumulate into one result
order = []

def f_multi_new():
    try:
        raise ExceptionGroup("m", [ValueError(1), KeyError(2)])
    except* ValueError as e:
        raise KeyError("h1")
    except* KeyError as e:
        order.append("h2")
        raise RuntimeError("h2")

e = probe(f_multi_new)
assert order == ["h2"]
assert str(e) == " (2 sub-exceptions)"
assert [type(s).__name__ for s in e.exceptions] == ["KeyError", "RuntimeError"]

order.clear()

def f_multi_bare():
    try:
        raise ExceptionGroup("m", [ValueError(1), KeyError(2)])
    except* ValueError:
        raise
    except* KeyError as e:
        order.append("h2")

e = probe(f_multi_bare)
assert order == ["h2"]
assert str(e) == "m (1 sub-exception)"
assert [type(s).__name__ for s in e.exceptions] == ["ValueError"]
print("ok")
