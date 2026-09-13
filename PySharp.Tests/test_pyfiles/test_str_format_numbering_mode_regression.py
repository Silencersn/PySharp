"""
Regression: str.format must reject mixing automatic '{}' and manual '{N}'
numbering with CPython's ValueError, instead of silently binding automatic
fields to argument 0. The mode is shared with nested format-spec fields,
while keyword names never participate in the check.

CPython 3.14 reference:
    '{0} {}'.format('a', 'b') -> ValueError: cannot switch from manual
                                 field specification to automatic field numbering
    '{} {0}'.format('a', 'b') -> ValueError: cannot switch from automatic
                                  field numbering to manual field specification
    '{0:>{}}'.format('a', 5)  -> ValueError (mode shared with nested specs)
    '{}{x}'.format(1, x=2)    -> '12' (kwargs are exempt)
"""

try:
    '{0} then {}'.format('a', 'b')
except ValueError as e:
    assert str(e) == 'cannot switch from manual field specification to automatic field numbering'
else:
    raise AssertionError('manual -> automatic must raise ValueError')

try:
    '{} then {0}'.format('a', 'b')
except ValueError as e:
    assert str(e) == 'cannot switch from automatic field numbering to manual field specification'
else:
    raise AssertionError('automatic -> manual must raise ValueError')

try:
    '{} {0} {}'.format(1, 2, 3)
except ValueError as e:
    assert str(e) == 'cannot switch from automatic field numbering to manual field specification'
else:
    raise AssertionError('auto, manual, auto must raise ValueError')

# the mode is shared with nested format-spec fields
try:
    '{0:>{}}'.format('a', 5)
except ValueError as e:
    assert str(e) == 'cannot switch from manual field specification to automatic field numbering'
else:
    raise AssertionError('nested automatic field after manual must raise ValueError')

# legal combinations keep working
assert '{} then {}'.format('a', 'b') == 'a then b'
assert '{0} then {1}'.format('a', 'b') == 'a then b'
assert '{0}-{0}'.format('a') == 'a-a'
assert '{:>{}}'.format('a', 5) == '    a'
assert '{0:>{1}}'.format('a', 5) == '    a'

# keyword names never participate in the mode check
assert '{}{x}'.format(1, x=2) == '12'
assert '{0}{x}'.format(1, x=2) == '12'
assert '{x}{}'.format(1, x=2) == '21'
assert '{x}{0}'.format(1, x=2) == '21'

print("test_str_format_numbering_mode_regression passed")
