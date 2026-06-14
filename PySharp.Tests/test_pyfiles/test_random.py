"""
Tests for the random module
"""
import random

# Module-level functions
r1 = random.random()
assert 0.0 <= r1 < 1.0
assert isinstance(r1, float)

u = random.uniform(1.0, 10.0)
assert 1.0 <= u <= 10.0

rr = random.randrange(10)
assert 0 <= rr < 10

ri = random.randint(1, 6)
assert 1 <= ri <= 6

# Random class with seed
r = random.Random(42)
r1a = r.random()
r1b = r.random()
assert r1a != r1b  # Different values
assert 0.0 <= r1a < 1.0

# Seeded Random should be deterministic
r2 = random.Random(42)
assert r2.random() == r1a
assert r2.random() == r1b

# Random.randint with seed
r3 = random.Random(123)
v1 = r3.randint(1, 100)
v2 = r3.randint(1, 100)
assert 1 <= v1 <= 100
assert 1 <= v2 <= 100

# Random.uniform
r4 = random.Random(42)
u1 = r4.uniform(5.0, 10.0)
assert 5.0 <= u1 <= 10.0

# Random.randrange
r5 = random.Random(42)
assert 0 <= r5.randrange(10) < 10
assert 5 <= r5.randrange(5, 10) < 10
assert 1 <= r5.randrange(1, 11, 2) <= 10  # odd numbers

print("test_random passed")
