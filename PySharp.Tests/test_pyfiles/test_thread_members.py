"""Verifies the threading.Thread member family: name/ident/native_id/daemon properties with
their CPython coercion rules, the deprecated camelCase aliases, the __repr__ state machine,
the join() self-check, and the constructor's group/args/name acceptance rules.

:kind: test
:background: only start/join/is_alive/run were wired; the member family the
    issue survey listed (name, ident, native_id, daemon, the camelCase
    aliases and __repr__) was absent entirely.
"""

import threading
import time
import warnings

# --- name property: get, str()-coerced set, constructor coercion ---
t = threading.Thread(target=lambda: None)
assert t.name.startswith("Thread-"), t.name
t.name = 456
assert t.name == "456", t.name
t.name = "worker"

t2 = threading.Thread(target=lambda: None, name=123)
assert t2.name == "123", t2.name
t3 = threading.Thread(target=lambda: None, name="")
# a falsy name falls back to the auto name, target suffix included
assert t3.name.startswith("Thread-") and "(<lambda>)" in t3.name, t3.name

# --- ident/native_id: None before start, equal ints after ---
assert t.ident is None, t.ident
assert t.native_id is None, t.native_id
t.start()
assert isinstance(t.ident, int) and t.ident != 0
assert t.native_id == t.ident
t.join()
assert t.ident is not None  # survives the thread's exit

# --- is_alive spans the run: alive while sleeping, dead after join ---
def slow():
    time.sleep(0.3)

t8 = threading.Thread(target=slow)
t8.start()
time.sleep(0.05)
assert t8.is_alive() is True, t8.is_alive()
t8.join()
assert t8.is_alive() is False, t8.is_alive()

# --- repr state machine: initial / started / stopped ---
fresh = threading.Thread(target=lambda: None)
assert repr(fresh) == f"<Thread({fresh.name}, initial)>", repr(fresh)
dt = threading.Thread(target=lambda: None, daemon=True)
assert repr(dt) == f"<Thread({dt.name}, initial daemon)>", repr(dt)
assert repr(t) == f"<Thread({t.name}, stopped {t.ident})>", repr(t)

def report_self(box):
    box.append(repr(box[0]))

box2 = [None]
t7 = threading.Thread(target=report_self, args=(box2,))
box2[0] = t7
t7.start()
t7.join()
assert box2[1] == f"<Thread({t7.name}, started {t7.ident})>", box2

# --- daemon: default False, stored verbatim, setter gated on start ---
assert threading.Thread(target=lambda: None).daemon is False
d2 = threading.Thread(target=lambda: None, daemon=1)
assert d2.daemon == 1 and d2.daemon is not True  # stored verbatim
d2.daemon = "yes"
assert d2.daemon == "yes"
try:
    t.daemon = False  # t already ran: _started stays set forever
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert str(e) == "cannot set daemon status of active thread", str(e)

# --- deprecated camelCase aliases warn once each ---
with warnings.catch_warnings(record=True) as w:
    warnings.simplefilter("always")
    t4 = threading.Thread(target=lambda: None)
    assert t4.getName() == t4.name
    t4.setName("renamed")
    assert t4.name == "renamed"
    assert t4.isDaemon() is False
    t4.setDaemon(True)
    assert t4.daemon is True
assert [x.category.__name__ for x in w] == ["DeprecationWarning"] * 4, w

# --- join() refuses the current thread ---
def self_join(box):
    try:
        box[0].join()
    except RuntimeError as e:
        box.append(str(e))

box = [None]
t5 = threading.Thread(target=self_join, args=(box,))
box[0] = t5
t5.start()
t5.join()
assert box[1] == "cannot join current thread", box

# --- a finished thread can be joined again, also with timeout 0 ---
t.join()
t.join(0)

# --- group is asserted away; args accepts lists ---
try:
    threading.Thread(group=1)
    raise AssertionError("expected AssertionError")
except AssertionError as e:
    assert str(e) == "group argument must be None for now", str(e)

hits = []
t6 = threading.Thread(target=hits.append, args=[7])
t6.start()
t6.join()
assert hits == [7], hits

print("thread members ok")
