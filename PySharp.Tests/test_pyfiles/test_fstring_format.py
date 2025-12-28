class MyClass:
	def __format__(self, value):
		return value

t1 = 1
t2 = 2
t3 = 3
txt = f'{MyClass()=:t1={t1};t2={t2}:t3={t3}}'
assert txt == 'MyClass()=t1=1;t2=2:t3=3'

try:
	txt = f'{MyClass()=!r:t1={t1};t2={t2}:t3={t3}}'
	assert False
except BaseException as e:
	assert isinstance(e, ValueError)
