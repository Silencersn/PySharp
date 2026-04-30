"""
Tests for round and vars
"""

# test round
assert round(3.14159, 2) == 3.14
assert round(3.14159) == 3
assert round(2.5) == 2
assert round(3.5) == 4
assert round(1234.5678, -2) == 1200.0

# test vars
assert type(vars()) is dict
assert "round" in vars()["__builtins__"].__dict__

class A:
    def __init__(self):
        self.x = 1
        self.y = 2

a = A()
v = vars(a)
assert v["x"] == 1
assert v["y"] == 2
v["x"] = 3
assert a.x == 3

print("test_round_vars passed")
