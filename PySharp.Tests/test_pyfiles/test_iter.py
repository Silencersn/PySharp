class A:
	def __init__(self):
		self.num = 5

	def __getitem__(self, item):
		self.num -= 1
		if self.num == 0:
			raise IndexError
		return self.num


itr = iter(A())
assert list(itr) == [4, 3, 2, 1]
