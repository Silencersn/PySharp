assert abs(-5) == 5
assert all([1, 2, 3]) is True
assert all([1, 0, 3]) is False
assert any([0, 0, 3]) is True
assert any([0, 0, 0]) is False
assert callable(len) is True
assert callable(123) is False
assert chr(65) == 'A'
assert dir() and isinstance(dir(), list)
assert isinstance(divmod(7, 3), tuple) and divmod(7, 3) == (2, 1)
assert eval('1+2') == 3
exec('assert True')
x = 123
assert 'x' in locals() and x == 123
assert globals()['__name__'] == '__main__' or '__name__' in globals()
assert hasattr([1, 2, 3], 'append') is True
assert hasattr([1, 2, 3], 'not_exist') is False
assert hash(123) == (123).__hash__()
assert id(123) == (123).__hash__() or isinstance(id(123), int)
assert isinstance(iter([1, 2, 3]), type(iter([])))
assert len([1, 2, 3]) == 3
assert max([1, 5, 3]) == 5
assert max(1, 5, 3) == 5
assert min([1, 5, 3]) == 1
assert min(1, 5, 3) == 1
assert next(iter([1, 2, 3])) == 1
assert next(iter([1]), 99) == 1
assert next(iter([]), 99) == 99
assert ord('A') == 65
assert pow(2, 3) == 8
assert pow(2, 3, 5) == 3
assert repr(123) == '123'
assert sum([1, 2, 3]) == 6
assert sum([1, 2, 3], 10) == 16
assert getattr([1, 2, 3], 'append', None) is not None
class MyObj: pass
o = MyObj()
setattr(o, 'foo', 42)
assert hasattr(o, 'foo') and getattr(o, 'foo') == 42
print(1, 2, 3, sep='a', end='', flush=True)
assert max([], default=1) == 1
assert min([], default=1) == 1
assert dir(0)
assert isinstance(1, (float, int))
assert issubclass(int, (float, int))
assert list(zip([1, 2, 3], [4, 5, 6])) == [(1, 4), (2, 5), (3, 6)]

try:
	list(zip([1, 2], [3], strict=True))
	assert False
except ValueError:
	pass

assert bin(0b_00001111_11110000_10101010) == '0b11111111000010101010'
assert bin(-0b_00001111_11110000_10101010) == '-0b11111111000010101010'

assert oct(0b_00001111_11110000_10101010) == '0o3770252'
assert oct(-999999999999999999999999999) == '-0o635456171177204003634777777777'

assert hex(100) == '0x64'
assert hex(-0b_00001111_11110000_10101010) == '-0xff0aa'