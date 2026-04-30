"""
Tests for f-string formatting using __format__
"""

class MyClass:
	def __format__(self, value):
		# Simply return the format spec for verification
		return value

t1 = 1
t2 = 2
t3 = 3
# Test passing complex format specifier through f-string
txt = f'{MyClass()=:t1={t1};t2={t2}:t3={t3}}'
assert txt == 'MyClass()=t1=1;t2=2:t3=3'

# Test that combining conversion (!r) with format spec might raise error if not supported
try:
	txt = f'{MyClass()=!r:t1={t1};t2={t2}:t3={t3}}'
	assert False, "Should have raised an error"
except ValueError:
	pass

# Test basic int formatting
assert f'{123:d}' == '123'
assert f'{123:5d}' == '  123'
assert f'{123:<5d}' == '123  '
assert f'{123:>5d}' == '  123'
assert f'{123:^5d}' == ' 123 '
assert f'{123:05d}' == '00123'
assert f'{123:0=5d}' == '00123'
assert f'{-123:05d}' == '-0123'
assert f'{123:+05}' == '+0123'
assert f'{123: x}' == ' 7b'
assert f'{123:X}' == '7B'
assert f'{123:#x}' == '0x7b'
assert f'{123:#X}' == '0X7B'
assert f'{123:#b}' == '0b1111011'
assert f'{123:#o}' == '0o173'
assert f'{123:c}' == '{'
assert f'{1234567:,}' == '1,234,567'
assert f'{1234567:_}' == '1_234_567'

# Test basic float formatting
assert f'{12.34:f}' == '12.340000'
assert f'{12.34:.1f}' == '12.3'
assert f'{12.34:10.2f}' == '     12.34'
assert f'{12.34:010.2f}' == '0000012.34'
assert f'{12.34:<10.2f}' == '12.34     '
assert f'{12.34:e}' == '1.234000e+01'
assert f'{12.34:E}' == '1.234000E+01'
assert f'{12.34:g}' == '12.34'
assert f'{12.34:%}' == '1234.000000%'
assert f'{1234567.89:,.2f}' == '1,234,567.89'
assert f'{1234567.89:_.2f}' == '1_234_567.89'

# Test falling back from int to float formats
assert f'{123:f}' == '123.000000'
assert f'{123:e}' == '1.230000e+02'
assert f'{123:%}' == '12300.000000%'

print("test_fstring_format passed")
