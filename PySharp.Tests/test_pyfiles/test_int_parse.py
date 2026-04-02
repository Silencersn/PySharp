"""
Integer parsing tests (various bases and formatting)
"""

assert int('123') == 123
assert int('-456') == -456
assert int('+789') == 789
assert int('0b1010', 2) == 10
assert int('0B1010', 2) == 10
assert int('0o77', 8) == 63
assert int('0O77', 8) == 63
assert int('0x1A', 16) == 26
assert int('0_1_2_3') == 123
assert int('0b1_0_1_0', 2) == 10
assert int('0x1_A', 16) == 26
assert int('0x_1_A', 16) == 26
assert int('0o7_7', 8) == 63
assert int('  42  ') == 42
assert int('0') == 0
assert int('-0') == 0
assert int('000123') == 123
assert int('0x10', 0) == 16
assert int('0b11', 0) == 3
assert int('0o11', 0) == 9
assert int('10', 10) == 10
assert int('A', 16) == 10
assert int('G', 17) == 16
try:
    int('')
    assert False
except ValueError:
    pass
try:
    int('0x1G', 16)
    assert False
except ValueError:
    pass
try:
    int('0x__1', 16)
    assert False
except ValueError:
    pass
try:
    int('0b102', 2)
    assert False
except ValueError:
    pass
try:
    int('0o78', 8)
    assert False
except ValueError:
    pass
try:
    int('0x', 16)
    assert False
except ValueError:
    pass
try:
    int('0b', 2)
    assert False
except ValueError:
    pass
try:
    int('0o', 8)
    assert False
except ValueError:
    pass
try:
    int('0_')
    assert False
except ValueError:
    pass
try:
    int('0__1')
    assert False
except ValueError:
    pass
try:
    int('A', 10)
    assert False
except ValueError:
    pass
try:
    int('10', 1)
    assert False
except ValueError:
    pass
try:
    int('10', 37)
    assert False
except ValueError:
    pass
try:
    int('0b_', 0)
    assert False
except ValueError:
    pass
try:
    int('+', 0)
    assert False
except ValueError:
    pass
try:
    int('_', 0)
    assert False
except ValueError:
    pass
try:
    int('\uFFFF', 0)
    assert False
except ValueError:
    pass
try:
    int('0b\uFFFF', 0)
    assert False
except ValueError:
    pass
try:
    int(None, None)
    assert False
except TypeError:
    pass

try:
    int(None)
    assert False
except TypeError:
    pass

    
try:
    int(None, 10)
    assert False
except TypeError:
    pass
