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

print("test_fstring_format passed")
