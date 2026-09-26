"""Verifies iter() drives the old-style __getitem__ iteration protocol: successive indices are requested and iteration stops when IndexError is raised, collecting the returned values.

:kind: test
"""

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
