"""
Property decorator tests (@property, setter, deleter)
"""

class Test:
	def __init__(self, value):
		self.value = value

	@property
	def test_attr(self):
		"""Getter for test_attr"""
		return self.value

	@test_attr.setter
	def test_attr(self, value):
		"""Setter for test_attr"""
		self.value = value

	@test_attr.deleter
	def test_attr(self):
		"""Deleter for test_attr"""
		self.value = None

# Test property operations
t = Test(5)
assert t.test_attr == 5

t.test_attr = 10
assert t.test_attr == 10
assert t.value == 10

del t.test_attr
assert t.value is None

print("test_property passed")

