"""
Tests for the queue module
"""
import queue

# Basic Queue operations
q = queue.Queue()
assert q.qsize() == 0
assert q.empty() is True
assert q.full() is False

# put and get
q.put(1)
q.put(2)
assert q.qsize() == 2
assert q.empty() is False

v1 = q.get()
v2 = q.get()
assert v1 == 1
assert v2 == 2
assert q.empty() is True

# put_nowait and get_nowait
q.put_nowait(10)
q.put_nowait(20)
assert q.qsize() == 2
assert q.get_nowait() == 10
assert q.get_nowait() == 20
assert q.empty() is True

# get_nowait on empty queue should raise Empty
try:
    q.get_nowait()
    assert False, "get_nowait() on empty should raise Empty"
except queue.Empty:
    pass

# Bounded queue
bq = queue.Queue(maxsize=2)
bq.put(1)
bq.put(2)
assert bq.full() is True

# put on full queue with block=False should raise Full
try:
    bq.put(3, block=False)
    assert False, "put() on full should raise Full"
except queue.Full:
    pass

# put_nowait on full queue should raise Full
try:
    bq.put_nowait(3)
    assert False, "put_nowait() on full should raise Full"
except queue.Full:
    pass

# task_done and join
q2 = queue.Queue()
q2.put(1)
q2.put(2)
q2.get()
q2.task_done()
q2.get()
q2.task_done()
q2.join()  # Should not block since all tasks done
assert q2.empty() is True

# task_done called too many times should raise ValueError
try:
    q2.task_done()
    assert False, "task_done() too many should raise ValueError"
except ValueError:
    pass

print("test_queue passed")
