"""Verifies the threading module surface: module constants (TIMEOUT_MAX,
ThreadError), the _MainThread object behind main_thread()/current_thread(),
active_count()/enumerate() registry views, the deprecated alias warnings,
stack_size validation and its query-resets quirk, the oversized-timeout
OverflowError domain on the waiting paths, and get_ident/get_native_id.

:kind: test
"""

import threading
import warnings

# module constants
print("TIMEOUT_MAX:", threading.TIMEOUT_MAX, type(threading.TIMEOUT_MAX).__name__)
print("ThreadError is RuntimeError:", threading.ThreadError is RuntimeError)

# the main thread object
mt = threading.main_thread()
ct = threading.current_thread()
print("main is current:", mt is ct)
print("main name:", mt.name)
print("main daemon:", mt.daemon)
print("main alive:", mt.is_alive())
print("main type:", type(mt).__name__, type(mt).__module__)
print("main subclass of Thread:", issubclass(type(mt), threading.Thread))
print("main isinstance:", isinstance(mt, threading.Thread))
# the repr embeds the ident (a per-run decimal), so pin its shape
r = repr(mt)
assert r.startswith("<_MainThread(MainThread, started "), r
assert r.endswith(">"), r
print("main repr shape: ok")
assert threading.get_ident() == mt.ident
assert threading.get_native_id() == mt.native_id
print("ident types:", type(threading.get_ident()).__name__, type(threading.get_native_id()).__name__)
print("active_count:", threading.active_count())
print("enumerate == [main]:", threading.enumerate() == [mt])
print("main_thread stable:", threading.main_thread() is mt)
print("callable start:", callable(mt.start))

# a thread cannot join itself, with or without a timeout
try:
    mt.join()
except RuntimeError as e:
    print("join self:", e)
try:
    mt.join(timeout=0.1)
except RuntimeError as e:
    print("join self timeout:", e)

# deprecated aliases still forward to the new names
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    assert threading.currentThread() is ct
    print("currentThread dep:", w[0].category.__name__, str(w[0].message))
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    assert threading.activeCount() == threading.active_count()
    print("activeCount dep:", w[0].category.__name__, str(w[0].message))

# stack_size: the query form resets to the default and returns the prior
# value; sizes below the platform minimum raise ValueError (the message
# text differs across 3.14 patch releases, so only the type is pinned)
print("stack default:", threading.stack_size())
for bad in (1, -1):
    try:
        threading.stack_size(bad)
        print("stack bad accepted:", bad)
    except ValueError:
        print("stack bad ValueError:", bad)
try:
    threading.stack_size("x")
except TypeError as e:
    print("stack str:", e)
try:
    threading.stack_size(10**30)
except OverflowError as e:
    print("stack huge:", e)
print("stack set:", threading.stack_size(53248))
print("stack get:", threading.stack_size())
print("stack after:", threading.stack_size())

# timeout domain: values past the WaitForSingleObject millisecond range
# fail before any waiting starts
lock = threading.Lock()
for probe in (4294967.3, 9223372037.0):
    try:
        lock.acquire(timeout=probe)
        print("lock timeout accepted:", probe)
    except OverflowError as e:
        print("lock timeout:", e)
sem = threading.Semaphore()
try:
    sem.acquire(timeout=4294967.3)
    print("sem timeout accepted")
except OverflowError as e:
    print("sem timeout:", e)
ev = threading.Event()
try:
    ev.wait(timeout=4294967.3)
    print("event timeout accepted")
except OverflowError as e:
    print("event timeout:", e)
ev.set()
print("event set wait:", ev.wait(timeout=4294967.3))

# the worker thread's view: main_thread is the main thread, the registry
# counts both, and get_ident/native_id agree with the object's fields
def worker():
    ct2 = threading.current_thread()
    print("worker: current is main:", ct2 is mt)
    print("worker: main_thread is main:", threading.main_thread() is mt)
    print("worker: active_count:", threading.active_count())
    names = sorted(t.name for t in threading.enumerate())
    print("worker: enumerate names:", names)
    assert threading.get_ident() == ct2.ident
    assert threading.get_native_id() == ct2.native_id
    inner = threading.Thread(target=lambda: None, name="inner")
    inner.start()
    inner.join()
    print("worker: after inner:", threading.active_count())

w = threading.Thread(target=worker, name="w")
w.start()
w.join()
print("after join: active_count:", threading.active_count())
print("after join: enumerate == [main]:", threading.enumerate() == [mt])

# a finished thread's join accepts an oversized timeout only through the
# same range check
try:
    w.join(timeout=4294967.3)
    print("join timeout accepted")
except OverflowError as e:
    print("join timeout:", e)
