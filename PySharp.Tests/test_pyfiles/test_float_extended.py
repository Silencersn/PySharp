"""
Tests for extended float methods
"""

# --- conjugate ---
assert (3.5).conjugate() == 3.5
assert (-2.0).conjugate() == -2.0
assert float('inf').conjugate() == float('inf')
assert float('-inf').conjugate() == float('-inf')
result = float('nan').conjugate()  # nan conjugate returns nan

# --- is_integer ---
assert (3.0).is_integer() is True
assert (3.5).is_integer() is False
assert (-2.0).is_integer() is True
assert (0.0).is_integer() is True
assert float('inf').is_integer() is False
assert float('nan').is_integer() is False

# --- real / imag ---
assert (3.5).real == 3.5
assert (3.5).imag == 0.0
assert (-2.0).real == -2.0
assert (-2.0).imag == 0.0

# --- as_integer_ratio ---
assert (3.5).as_integer_ratio() == (7, 2)
assert (2.0).as_integer_ratio() == (2, 1)
assert (0.0).as_integer_ratio() == (0, 1)
assert (-1.5).as_integer_ratio() == (-3, 2)

try:
    float('inf').as_integer_ratio()
    assert False, 'should raise ValueError'
except ValueError:
    pass

try:
    float('nan').as_integer_ratio()
    assert False, 'should raise ValueError'
except ValueError:
    pass

# --- hex / fromhex ---
assert (1.0).hex() == '0x1.0000000000000p+0'
assert (-1.0).hex() == '-0x1.0000000000000p+0'
assert (0.0).hex() == '0x0.0p+0'
assert (3.5).hex() == '0x1.c000000000000p+1'
assert (0.5).hex() == '0x1.0000000000000p-1'

# fromhex
assert float.fromhex('0x1.0000000000000p+0') == 1.0
assert float.fromhex('-0x1.0000000000000p+0') == -1.0
assert float.fromhex('0x1.c000000000000p+1') == 3.5
assert float.fromhex('0x1.0000000000000p-1') == 0.5

try:
    float.fromhex('invalid')
    assert False, 'should raise ValueError'
except ValueError:
    pass

print("test_float_extended passed")
