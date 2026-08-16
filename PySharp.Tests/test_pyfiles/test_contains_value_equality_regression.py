"""
Regression: list/tuple `in` must use Python value equality (CPython
semantics), not .NET reference equality, so equal-but-distinct elements
are found.

CPython 3.14 reference:
    (1, 2) in [(1, 2)]           -> True
    [1] in [[1], [2]]            -> True
    (1, 2) in ((1, 2), (3, 4))   -> True
    'apple' in ['apple', 'mango'] -> True
    1 in [1, 2, 3]               -> True
    (1, 2) in [(3, 4)]           -> False
    1 in ['1']                   -> False
    float('nan') in [float('nan')] -> False
"""

# --- Value equality for distinct instances (core regression) ---

# Tuples with equal values but distinct instances.
pair = (1, 2)
assert pair in [(1, 2), (3, 4)]
assert pair not in [(3, 4), (5, 6)]
assert pair in ((1, 2), (3, 4))
assert pair not in ()

# Nested lists with equal values but distinct instances.
inner = [1]
assert inner in [[1], [2]]
assert inner not in [[2], [3]]

# Strings built at runtime so they are distinct instances.
name = "ap" + "ple"
assert name in ["apple", "mango"]
assert name not in ["mango", "orange"]

# Int/float/bool cross-type equality (1 == 1.0 == True).
assert 1 in [1.0, 2.0]
assert 1.0 in [1, 2]
assert True in [1, 2]
assert 1 in [True, 2]

# NaN never equals anything, including itself.
assert float("nan") not in [float("nan"), 0.0]
assert 0.0 not in [float("nan"), 1.0]

# --- Operand order: `item in container` checks `element == item` ---
# (the container element is the left operand of the equality test)

calls = []

class LeftEq:
    def __eq__(self, other):
        calls.append("LeftEq")
        return True

class RightEq:
    def __eq__(self, other):
        calls.append("RightEq")
        return True

# The element (LeftEq) must be the left operand, so its __eq__ runs first.
del calls[:]
assert (RightEq() in [LeftEq()]) is True
assert calls == ["LeftEq"], f"operand order wrong: {calls}"

# --- Reflected fallback when the element's __eq__ returns NotImplemented ---

class Defer:
    def __eq__(self, other):
        calls.append("Defer")
        return NotImplemented

class Decides:
    def __eq__(self, other):
        calls.append("Decides")
        return True

# Element (Defer) defers -> reflected Decides.__eq__(Defer) -> True.
del calls[:]
assert (Decides() in [Defer()]) is True
assert calls == ["Defer", "Decides"], f"reflected fallback wrong: {calls}"

# --- Both sides return NotImplemented -> identity fallback (False here) ---

class Neither:
    def __eq__(self, other):
        return NotImplemented

assert (Neither() in [Neither()]) is False

print("test_contains_value_equality_regression passed")
