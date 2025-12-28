class Test:
	def init(self, value):
		self.value = value

	@property
	def test_property(self):
		return self.value

	@test_property.setter
	def test_property(self, value):
		self.value = value

	@test_property.deleter
	def test_property(self):
		self.value = None

test = Test()
test.init(5)
assert test.test_property == 5
test.init(4)
assert test.test_property == 4
test.test_property = 3
assert test.test_property == 3
del test.test_property
assert test.value is None

