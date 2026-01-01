class MyClass:
	def __getitem__(self, item):
		assert isinstance(item, slice)

MyClass()[1:2:3]


class MyClass2:
	def __getitem__(self, item):
		assert isinstance(item[0], slice)
		assert isinstance(item[1], slice)

MyClass2()[1:2:3, 4:5:6, ]