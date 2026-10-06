"""Verifies the threading module surface: Timer construction attributes
(interval/args/kwargs stored verbatim, finished event), firing and both
cancel paths, threading.local per-thread isolation (attributes do not
carry over, subclass __init__ replays with construction arguments in each
fresh thread), and the excepthook channel (the _ExceptHookArgs fields a
custom hook receives, module-level replacement, a raising hook being
reported and swallowed).

:kind: test
"""

import threading

# --- Timer ---
print("Timer subclass:", issubclass(threading.Timer, threading.Thread))
print("Timer name:", threading.Timer.__name__)

fired = []
tm = threading.Timer(0.15, fired.append, args=(42,))
print("interval:", tm.interval)
print("args:", tm.args)
print("kwargs:", tm.kwargs)
print("finished before:", tm.finished.is_set())
print("alive before:", tm.is_alive())
print("repr shape:", repr(tm).startswith("<Timer(Thread-"))
tm.start()
tm.join()
print("fired:", fired)
print("finished after:", tm.finished.is_set())

results = []
tmk = threading.Timer(0.05, lambda a, b=0: results.append((a, b)), args=(1,), kwargs={"b": 2})
tmk.start()
tmk.join()
print("kwargs call:", results)

# cancel before fire stops the function from running
cancel_ran = []
tc = threading.Timer(30.0, lambda: cancel_ran.append(1))
tc.start()
tc.cancel()
tc.join(timeout=5)
print("cancel fired:", cancel_ran, "alive:", tc.is_alive())

# cancel after fire is a no-op
tl = threading.Timer(0.05, lambda: None)
tl.start()
tl.join()
tl.cancel()
print("late cancel finished:", tl.finished.is_set())

# --- local ---
print("local names:", threading.local.__name__, threading.local.__module__)
loc = threading.local()
print("instance type:", type(loc).__name__, type(loc).__module__)

seen = {}
def local_worker():
    # values are thread-local: the creating thread's attributes are NOT visible
    seen["has x"] = hasattr(loc, "x")
    try:
        loc.x
    except AttributeError as e:
        seen["attr error"] = type(e).__name__
    loc.x = 99
    seen["own set"] = loc.x
    seen["dict keys"] = sorted(loc.__dict__.keys())

w = threading.Thread(target=local_worker)
w.start()
w.join()
print("worker has x:", seen["has x"], seen["attr error"])
print("worker own:", seen["own set"], seen["dict keys"])

loc.y = "two"
loc.x = 1
print("main sees:", loc.x, loc.y)
print("main dict:", sorted(loc.__dict__.keys()))
try:
    loc.missing
except AttributeError as e:
    print("missing attr:", type(e).__name__)

try:
    threading.local(1)
except TypeError as e:
    print("local args:", type(e).__name__, e)

# subclass __init__ runs once per thread that touches the local
class MyLocal(threading.local):
    def __init__(self):
        self.init_runs = getattr(self, "init_runs", 0) + 1

ml = MyLocal()
print("main init:", ml.init_runs)
counts = []
def subclass_worker():
    counts.append(ml.init_runs)
    ml.tag = "w"
    counts.append(sorted(ml.__dict__.keys()))
w2 = threading.Thread(target=subclass_worker)
w2.start()
w2.join()
print("worker init:", counts)
print("main init after:", ml.init_runs)

# construction arguments are replayed in each fresh thread
class ArgLocal(threading.local):
    def __init__(self, stamp):
        self.stamp = stamp

al = ArgLocal(7)
print("arg local main:", al.stamp)
w3_seen = []
def arg_worker():
    w3_seen.append(al.stamp)
w3 = threading.Thread(target=arg_worker)
w3.start()
w3.join()
print("arg local worker:", w3_seen)

# --- excepthook ---
captured = []
def hook(args):
    captured.append(args)
    print("exc_type:", args.exc_type.__name__)
    print("exc_value:", type(args.exc_value).__name__, str(args.exc_value))
    print("thread name:", args.thread.name)
    print("len:", len(args))
    print("indexed:", args[0] is args.exc_type, args[3] is args.thread)
    r = repr(args)
    print("repr prefix:", r.startswith("_thread._ExceptHookArgs("))
    print("args type name:", type(args).__name__)

orig = threading.excepthook
threading.excepthook = hook
def boom():
    raise ValueError("worker boom")
t = threading.Thread(target=boom, name="boomthread")
t.start()
t.join()
threading.excepthook = orig
print("captured:", len(captured))
print("excepthook name:", threading.excepthook.__name__)

# a hook that itself raises: threading reports it and swallows the original
def bad_hook(args):
    raise RuntimeError("hook broken")
threading.excepthook = bad_hook
def boom3():
    raise KeyError("k")
t3 = threading.Thread(target=boom3, name="hookfail")
t3.start()
t3.join()
threading.excepthook = orig
print("bad hook clean exit")
