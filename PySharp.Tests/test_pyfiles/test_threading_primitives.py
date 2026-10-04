"""Verifies the threading synchronization primitives: Lock/RLock acquisition
and timeout rules, Condition wait/notify (including the duck-typed lock
protocol), Semaphore/BoundedSemaphore counters, Event flag semantics, and the
cyclic Barrier state machine with BrokenBarrierError.

:kind: test
:background: the module exposed only the Thread class; the whole
    synchronization family (Lock, RLock, Condition, Semaphore,
    BoundedSemaphore, Event, Barrier, BrokenBarrierError) was missing.
"""

import threading
import time

# --- Lock: type identity, acquire rules, release errors ---
lk = threading.Lock()
print("lock repr:", repr(lk))
assert type(lk).__name__ == "lock", type(lk).__name__
assert type(lk).__module__ == "_thread", type(lk).__module__
assert lk.acquire() is True
print("lock held repr:", repr(lk))
assert lk.locked() is True
assert lk.acquire(False) is False
assert lk.acquire(True, 0.01) is False
lk.release()
assert lk.locked() is False
assert lk.acquire(False) is True
lk.release()

try:
    lk.release()
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "release unlocked lock", str(e)

try:
    lk.acquire(False, 1)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "can't specify a timeout for a non-blocking call", str(e)

try:
    lk.acquire(True, -2)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "timeout value must be a non-negative number", str(e)

with lk:
    assert lk.locked() is True
assert lk.locked() is False

# non-reentrant: a held lock is not reacquired by the same thread
lk.acquire()
assert lk.acquire(True, 0.01) is False
lk.release()

# timed acquire actually waits
lk.acquire()
start = time.time()
assert lk.acquire(True, 0.1) is False
elapsed = time.time() - start
assert 0.09 < elapsed < 1.0, elapsed
lk.release()

# --- RLock: reentrancy, owner checks ---
rl = threading.RLock()
print("rlock repr:", repr(rl))
assert type(rl).__name__ == "RLock", type(rl).__name__
assert type(rl).__module__ == "_thread", type(rl).__module__
assert rl.acquire() is True
assert rl.acquire() is True
assert rl.acquire() is True
assert rl._recursion_count() == 3, rl._recursion_count()
# the held repr embeds the owning thread's ident, which differs per run and
# per interpreter, so pin the shape instead of printing it
assert repr(rl).startswith("<locked _thread.RLock object owner="), repr(rl)
assert " count=3 at 0x" in repr(rl), repr(rl)
rl.release()
rl.release()
assert rl.locked() is True
rl.release()
assert rl.locked() is False
assert rl._recursion_count() == 0

try:
    rl.release()
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "cannot release un-acquired lock", str(e)

box = []
def foreign_release(box, rlock):
    try:
        rlock.release()
    except RuntimeError as e:
        box.append(str(e))

rl.acquire()
t = threading.Thread(target=foreign_release, args=(box, rl))
t.start()
t.join()
assert box == ["cannot release un-acquired lock"], box
rl.release()

with rl:
    with rl:
        assert rl.locked() is True
assert rl.locked() is False

# --- Condition: default lock, ownership guards, wait/notify ---
cv = threading.Condition()
print("cond repr:", repr(cv))
assert repr(cv).startswith("<Condition(<unlocked _thread.RLock object owner=0 count=0"), repr(cv)

try:
    cv.wait()
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "cannot wait on un-acquired lock", str(e)

try:
    cv.notify()
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "cannot notify on un-acquired lock", str(e)

# wait returns True when notified
results = []
def waiter(cv, results):
    with cv:
        results.append(("entered", cv.wait(2)))

w = threading.Thread(target=waiter, args=(cv, results))
w.start()
time.sleep(0.05)
with cv:
    cv.notify()
w.join()
assert results[0][0] == "entered" and results[0][1] is True, results

# notify wakes exactly one waiter; notify_all wakes the rest
woken = []
def nwaiter(cv, woken):
    with cv:
        cv.wait()
        woken.append(1)

threads = [threading.Thread(target=nwaiter, args=(cv, woken)) for _ in range(3)]
for th in threads:
    th.start()
time.sleep(0.1)
with cv:
    cv.notify()
time.sleep(0.1)
assert len(woken) == 1, woken
with cv:
    cv.notify_all()
for th in threads:
    th.join()
assert len(woken) == 3, woken

# wait timeout returns False but re-acquires the lock
probe = {}
def timeout_waiter(cv, probe):
    with cv:
        probe["got"] = cv.wait(0.02)
        probe["owned"] = cv._is_owned()

tw = threading.Thread(target=timeout_waiter, args=(cv, probe))
tw.start()
tw.join()
assert probe == {"got": False, "owned": True}, probe

# wait_for polls a predicate until it flips
state = {"v": 0}
def predicate_waiter(cv, state, out):
    with cv:
        out.append(cv.wait_for(lambda: state["v"] == 1, timeout=2))

out = []
pw = threading.Thread(target=predicate_waiter, args=(cv, state, out))
pw.start()
time.sleep(0.05)
with cv:
    state["v"] = 1
    cv.notify_all()
pw.join()
assert out == [True], out

# a duck-typed lock rides along through the protocol
class DuckLock:
    def __init__(self):
        self._l = threading.Lock()
        self.acquires = 0
    def acquire(self, *args):
        self.acquires += 1
        return self._l.acquire(*args)
    def release(self):
        return self._l.release()
    def locked(self):
        return self._l.locked()
    def __enter__(self):
        self.acquire()
        return self
    def __exit__(self, *exc):
        self.release()

duck = DuckLock()
dcv = threading.Condition(duck)
with dcv:
    dcv.notify()
assert duck.acquires >= 1, duck.acquires

# --- Semaphore: counters, non-blocking and timed acquire ---
sem = threading.Semaphore(3)
print("sem repr:", repr(sem))
assert sem.acquire() is True
assert sem.acquire(blocking=False) is True
assert type(sem).__module__ == "threading", type(sem).__module__
assert sem._value == 1, sem._value
sem.release(2)
assert sem._value == 3, sem._value

empty = threading.Semaphore(0)
assert empty.acquire(False) is False
assert empty.acquire(True, 0.02) is False
try:
    empty.acquire(False, 1)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "can't specify timeout for non-blocking acquire", str(e)

try:
    threading.Semaphore(-1)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "semaphore initial value must be >= 0", str(e)

try:
    sem.release(0)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "n must be one or more", str(e)

# producer/consumer shape: release wakes a blocked acquirer
got = []
def consumer(empty, got):
    if empty.acquire(True, 2):
        got.append(1)

c = threading.Thread(target=consumer, args=(empty, got))
c.start()
time.sleep(0.05)
empty.release()
c.join()
assert got == [1], got

# --- BoundedSemaphore: inheritance and the release ceiling ---
bounded = threading.BoundedSemaphore(2)
print("bounded repr:", repr(bounded))
assert isinstance(bounded, threading.Semaphore), "BoundedSemaphore subclasses Semaphore"
assert bounded.acquire() is True
assert bounded.acquire() is True
bounded.release(2)
try:
    bounded.release()
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "Semaphore released too many times", str(e)

# --- Event: flag semantics and waits ---
ev = threading.Event()
print("event repr:", repr(ev))
assert ev.is_set() is False
assert ev.wait(0.01) is False
ev.set()
print("event set repr:", repr(ev))
assert ev.is_set() is True
assert ev.wait() is True
assert ev.wait(0.01) is True
ev.clear()
assert ev.is_set() is False

flag = []
def event_waiter(ev, flag):
    if ev.wait(2):
        flag.append(1)

ew = threading.Thread(target=event_waiter, args=(ev, flag))
ew.start()
time.sleep(0.05)
ev.set()
ew.join()
assert flag == [1], flag

# --- Barrier: cycles, action callbacks, breaking ---
acts = []
bar = threading.Barrier(2, action=lambda: acts.append("act"))
print("barrier repr:", repr(bar))
assert bar.parties == 2, bar.parties
assert bar.n_waiting == 0, bar.n_waiting
assert bar.broken is False

indices = []
def barrier_worker(bar, indices):
    indices.append(bar.wait())

bw = threading.Thread(target=barrier_worker, args=(bar, indices))
bw.start()
main_index = bar.wait()
bw.join()
assert sorted(indices + [main_index]) == [0, 1], (indices, main_index)
assert acts == ["act"], acts
assert bar.broken is False
assert bar.n_waiting == 0, bar.n_waiting

# a second cycle reuses the same barrier
indices2 = []
bw2 = threading.Thread(target=barrier_worker, args=(bar, indices2))
bw2.start()
main2 = bar.wait()
bw2.join()
assert sorted(indices2 + [main2]) == [0, 1]
assert acts == ["act", "act"], acts

# a timeout breaks the barrier for everyone
bto = threading.Barrier(2, timeout=0.05)
outcomes = []
def barrier_timeout_worker(bto, outcomes):
    try:
        bto.wait()
        outcomes.append("passed")
    except threading.BrokenBarrierError:
        outcomes.append("broke")

btw = threading.Thread(target=barrier_timeout_worker, args=(bto, outcomes))
btw.start()
btw.join()
assert outcomes == ["broke"], outcomes
assert bto.broken is True, bto.broken
bto.reset()
assert bto.broken is False

try:
    threading.Barrier(0)
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "parties must be >= 1", str(e)

assert issubclass(threading.BrokenBarrierError, RuntimeError)
assert threading.BrokenBarrierError.__module__ == "threading", threading.BrokenBarrierError.__module__

print("threading primitives ok")
