"""
Regression: str.startswith/endswith with a negative 'end' must map end to
len+end (like slicing) before comparing.

CPython 3.14 reference:
    'hello'.startswith('h', 0, -1)   -> True    ([0:4] = 'hell')
    'hello'.startswith('l', -3, -1)  -> True    ([2:4] = 'll')
    'hello'.startswith('', 0, -1)    -> True
    'hello'.startswith('h', 0, -100) -> False   (end clamped to 0)
    'hello'.endswith('h', 0, -4)     -> True    ([0:1] = 'h')
    'hello'.endswith('', 0, -1)      -> True
    'hello'.endswith('ll', -3, -1)   -> True    ([2:4] = 'll')

Previously PySharp only upper-clamped end (end > len -> len) and never mapped
negative end to len+end, so 'start >= end' returned False prematurely.
"""

assert 'hello'.startswith('h', 0, -1)
assert 'hello'.startswith('l', -3, -1)
assert 'hello'.startswith('', 0, -1)
assert not 'hello'.startswith('h', 0, -100)

assert 'hello'.endswith('h', 0, -4)
assert 'hello'.endswith('', 0, -1)
assert 'hello'.endswith('ll', -3, -1)

# positive/zero boundaries keep working
assert 'hello'.startswith('h')
assert 'hello'.startswith('h', 0, 1)
assert not 'hello'.startswith('h', 0, 0)
assert 'hello'.endswith('o')
assert 'hello'.endswith('o', 0, 5)

print("test_str_startswith_negative_end_regression passed")
