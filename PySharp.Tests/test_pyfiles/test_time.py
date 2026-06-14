"""
Tests for the time module
"""
import time

# time() returns current timestamp as float
t1 = time.time()
assert isinstance(t1, float)
assert t1 > 1_700_000_000  # Should be > 2023 (Unix timestamp)

# Multiple calls should return different values (time advances)
t2 = time.time()
assert t2 >= t1

# Basic range check
assert t1 > 0

print("test_time passed")
