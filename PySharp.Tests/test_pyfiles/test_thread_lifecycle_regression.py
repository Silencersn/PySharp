# Regression for #139: threading.Thread lifecycle semantics.
#
# 1. is_alive() on a never-started thread must return False (CPython
#    threading.py:1177 gates on the started event) instead of piercing the
#    process with a .NET InvalidOperationException.
# 2. start() dispatches through an ordinary self.run attribute lookup
#    (CPython threading.py:1082), so a subclass override replaces the
#    default target call (threading.py:1013-1028).
# 3. double start() and join()-before-start() raise RuntimeError with the
#    CPython messages instead of .NET unhandled exceptions.

import threading

# --- is_alive before start: False, callable safely ---
t = threading.Thread(target=lambda: None)
assert t.is_alive() is False, t.is_alive()

# --- subclass run() override executes, with instance state ---
class Worker(threading.Thread):
    def run(self):
        self.ran = "yes"

w = Worker()
w.ran = "no"
w.start()
w.join()
assert w.ran == "yes", w.ran

# --- default target dispatch still works after the run() export ---
hits = []
t2 = threading.Thread(target=hits.append, args=(1,))
t2.start()
t2.join()
assert hits == [1], hits

# --- run() is directly callable like CPython's default run ---
calls = []
t5 = threading.Thread(target=calls.append, args=(2,))
t5.run()
assert calls == [2], calls

# --- double start: RuntimeError, not .NET crash ---
t3 = threading.Thread(target=lambda: None)
t3.start()
try:
    t3.start()
except RuntimeError as e:
    assert str(e) == "threads can only be started once", str(e)
else:
    raise AssertionError("double start did not raise")
t3.join()
assert t3.is_alive() is False, t3.is_alive()

# --- join before start: RuntimeError, not .NET crash ---
t4 = threading.Thread(target=lambda: None)
try:
    t4.join()
except RuntimeError as e:
    assert str(e) == "cannot join thread before it is started", str(e)
else:
    raise AssertionError("join before start did not raise")

# --- uncaught target exception goes through the thread excepthook
# channel (asserted from the C# side via stderr) and the main thread
# keeps running ---
def boom():
    raise ValueError("kaboom")

t6 = threading.Thread(target=boom)
t6.start()
t6.join()

print("thread lifecycle regression ok")
