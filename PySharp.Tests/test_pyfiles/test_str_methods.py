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

print("test_str_methods passed")
