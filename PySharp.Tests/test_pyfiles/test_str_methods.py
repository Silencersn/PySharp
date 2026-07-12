"""
Tests for str methods
"""

# upper/lower
assert 'hello'.upper() == 'HELLO'
assert 'HELLO'.lower() == 'hello'

# strip
assert ' hello   '.strip() == 'hello'
assert ' hello   '.lstrip() == 'hello   '
assert '   hello   '.rstrip() == '   hello'
assert 'www.example.com'.strip('cmowz.') == 'example'

# startswith / endswith
assert 'hello'.startswith('he') is True
assert 'hello'.startswith('o') is False
assert 'hello'.endswith('lo') is True
assert 'hello'.endswith('h') is False

# replace
assert 'hello'.replace('l', 'p') == 'heppo'
assert 'hello'.replace('l', 'p', 1) == 'heplo'
# assert 'hello'.replace('', '-') == '-h-e-l-l-o-'
# assert 'hello'.replace('', '-', 2) == '-h-ello'

# split
assert 'hello world'.split() == ['hello', 'world']
assert 'hello world'.split('o') == ['hell', ' w', 'rld']
assert 'hello world'.split('o', 1) == ['hell', ' world']

# find / rfind
assert 'hello'.find('l') == 2
assert 'hello'.rfind('l') == 3
assert 'hello'.find('x') == -1
assert 'hello'.rfind('x') == -1

assert 'hello'.capitalize() == 'Hello'
assert 'HELLO'.casefold() == 'hello'
assert 'hello'.center(10, '-') == '--hello---'

assert 'hello'.count('l') == 2
assert 'abc'.isalnum() is True
assert 'abc1'.isalpha() is False
assert '123'.isdigit() is True

assert 'hello'.islower() is True
assert 'HELLO'.isupper() is True

assert 'hello world'.title() == 'Hello World'
assert 'hElLo'.swapcase() == 'HeLlO'

assert 'hello'.zfill(10) == '00000hello'
assert '-123'.zfill(6) == '-00123'
assert '+123'.zfill(6) == '+00123'

# format
assert '{} {}'.format('hello', 'world') == 'hello world'
assert '{0} {1}'.format('a', 'b') == 'a b'
assert '{1} {0}'.format('a', 'b') == 'b a'
assert '{name} is {age}'.format(name='Alice', age=20) == 'Alice is 20'
assert '{}'.format(42) == '42'

# partition
assert 'hello'.partition('l') == ('he', 'l', 'lo')
assert 'hello'.partition('x') == ('hello', '', '')
assert 'hello'.partition('o') == ('hell', 'o', '')
assert 'hello'.partition('h') == ('', 'h', 'ello')

# splitlines
assert 'hello\nworld'.splitlines() == ['hello', 'world']
assert 'hello\nworld\n'.splitlines() == ['hello', 'world']
assert 'hello\r\nworld\r\n'.splitlines() == ['hello', 'world']
assert 'hello\nworld'.splitlines(True) == ['hello\n', 'world']
assert 'hello\n\nworld'.splitlines() == ['hello', '', 'world']
assert ''.splitlines() == []

# isspace
assert '   '.isspace() is True
assert '\t\n\r\v\f'.isspace() is True
assert 'hello'.isspace() is False
assert ''.isspace() is False
assert 'a b'.isspace() is False

# expandtabs
assert 'a\tb'.expandtabs(4) == 'a   b'
assert 'a\tb'.expandtabs(8) == 'a       b'
assert 'a\t\tb'.expandtabs(4) == 'a       b'
assert 'abc\td'.expandtabs(4) == 'abc d'
assert 'a\tb'.expandtabs(0) == 'ab'

# % operator
assert '%s %s' % ('hello', 'world') == 'hello world'
assert '%d + %d = %d' % (1, 2, 3) == '1 + 2 = 3'
assert '%x' % 255 == 'ff'
assert '%X' % 255 == 'FF'
assert '%r' % 'hello' == "'hello'"
assert '%.2f' % 3.14159 == '3.14'
assert '%10s' % 'hi' == '        hi'
assert '%-10s' % 'hi' == 'hi        '
assert '%05d' % 42 == '00042'
assert '%c' % 65 == 'A'
assert '%%' % () == '%'
assert '%(name)s is %(age)d' % {'name': 'Bob', 'age': 30} == 'Bob is 30'

# % operator - additional format types
assert '%u' % 42 == '42'
assert '%o' % 255 == '377'
assert '%#o' % 255 == '0o377'
# Note: %e/%E use .NET formatting (exponent with 3 digits, e.g. e+004)
assert '%e' % 12345.6789 == '1.234568e+004'
assert '%E' % 12345.6789 == '1.234568E+004'
assert '%.2e' % 12345.6789 == '1.23e+004'
assert '%g' % 12345.6789 == '12345.7'
assert '%G' % 12345.6789 == '12345.7'
assert '%#.0f' % 3.0 == '3.'
assert '%hi' % 42 == '42'  # h length modifier ignored

# str[i:j] slicing
assert 'hello'[0:3] == 'hel'
assert 'hello'[1:4] == 'ell'
assert 'hello'[:] == 'hello'
assert 'hello'[::-1] == 'olleh'
assert 'hello'[0:10] == 'hello'
assert 'hello'[10:20] == ''
assert 'hello'[-3:-1] == 'll'
assert 'hello'[-5:] == 'hello'
assert 'hello'[::2] == 'hlo'
assert 'abcdef'[1:5:2] == 'bd'

print("test_str_methods passed")
