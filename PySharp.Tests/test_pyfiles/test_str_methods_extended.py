"""
Tests for extended str methods
"""

# --- rsplit ---
assert 'hello world'.rsplit() == ['hello', 'world']
assert 'hello world'.rsplit('o') == ['hell', ' w', 'rld']
assert 'hello world'.rsplit('o', 1) == ['hello w', 'rld']
# rsplit with no sep splits whitespace from right
assert 'a  b  c'.rsplit() == ['a', 'b', 'c']
assert 'abc'.rsplit('x') == ['abc']
assert 'a b c'.rsplit(None, 1) == ['a b', 'c']

# --- index / rindex ---
assert 'hello'.index('l') == 2
assert 'hello'.rindex('l') == 3
try:
    'hello'.index('x')
    assert False, 'should raise ValueError'
except ValueError:
    pass

try:
    'hello'.rindex('x')
    assert False, 'should raise ValueError'
except ValueError:
    pass

# index/rindex with start/end
assert 'hello'.index('l', 3) == 3
assert 'hello'.rindex('l', 0, 3) == 2

# --- ljust / rjust ---
assert 'hello'.ljust(10) == 'hello     '
assert 'hello'.rjust(10) == '     hello'
assert 'hello'.ljust(10, '*') == 'hello*****'
assert 'hello'.rjust(10, '*') == '*****hello'
assert 'hello'.ljust(3) == 'hello'   # width <= len
assert 'hello'.rjust(3) == 'hello'

# --- rpartition ---
assert 'hello'.rpartition('x') == ('', '', 'hello')
assert 'hello'.rpartition('o') == ('hell', 'o', '')
assert 'hello'.rpartition('h') == ('', 'h', 'ello')
# rpartition picks rightmost occurrence
assert 'hello'.rpartition('l') == ('hel', 'l', 'o')

# --- removeprefix / removesuffix ---
assert 'hello'.removeprefix('hel') == 'lo'
assert 'hello'.removeprefix('xyz') == 'hello'
assert 'hello'.removesuffix('llo') == 'he'
assert 'hello'.removesuffix('xyz') == 'hello'
assert 'hello'.removeprefix('') == 'hello'
assert 'hello'.removesuffix('') == 'hello'

# --- isascii ---
assert 'hello'.isascii() is True
assert ''.isascii() is True
assert 'hello123'.isascii() is True
# Non-ASCII characters
assert '\u00e9'.isascii() is False
assert '\u4e2d'.isascii() is False

# --- istitle ---
assert 'Hello World'.istitle() is True
assert 'Hello World'.istitle() is True
assert 'HELLO'.istitle() is False
assert 'hello'.istitle() is False
assert 'H'.istitle() is True
assert 'h'.istitle() is False
assert ''.istitle() is False
assert 'Hello World!'.istitle() is True
assert 'Hello wOrld'.istitle() is False

# --- isdecimal ---
assert '123'.isdecimal() is True
assert '0'.isdecimal() is True
assert ''.isdecimal() is False
assert '12.3'.isdecimal() is False
assert 'abc'.isdecimal() is False
assert '\u0660'.isdecimal() is True     # Arabic-Indic digit 0
assert '\u00b2'.isdecimal() is False    # superscript 2 is not decimal

# --- isnumeric ---
assert '123'.isnumeric() is True
assert '\u00b2'.isnumeric() is True     # superscript 2
assert '\u00bd'.isnumeric() is True     # fraction 1/2
assert '\u0969'.isnumeric() is True     # Devanagari digit
assert ''.isnumeric() is False
assert 'abc'.isnumeric() is False

# --- isidentifier ---
assert 'hello'.isidentifier() is True
assert '_hello'.isidentifier() is True
assert 'hello123'.isidentifier() is True
assert '123hello'.isidentifier() is False
assert 'for'.isidentifier() is True     # keywords are identifiers
assert ''.isidentifier() is False
assert 'hello world'.isidentifier() is False
assert 'h'.isidentifier() is True

# --- isprintable ---
assert 'hello'.isprintable() is True
assert ''.isprintable() is True
assert 'hello world'.isprintable() is True
assert 'hello\nworld'.isprintable() is True     # Python considers \n printable
assert 'hello\tworld'.isprintable() is True     # \t is printable
assert '\x00'.isprintable() is False
assert '\x1b'.isprintable() is False

# --- encode with errors ---
assert 'hello'.encode('utf-8') == b'hello'
assert 'hello'.encode() == b'hello'

# --- startswith / endswith with start/end ---
assert 'hello world'.startswith('world', 6) is True
assert 'hello world'.startswith('hello', 0) is True
assert 'hello world'.startswith('world', 0) is False
assert 'hello world'.endswith('hello', 0, 5) is True
assert 'hello world'.endswith('world', 6) is True
assert 'hello world'.endswith('hello') is False

# --- find / rfind with start/end ---
assert 'hello world'.find('o', 5) == 7
assert 'hello world'.rfind('o', 0, 6) == 4
assert 'hello world'.find('x', 0, 5) == -1

# --- count with start/end ---
assert 'hello world'.count('o', 0, 5) == 1
assert 'hello world'.count('o', 5) == 1
assert 'hello world'.count('l', 0, 3) == 1
assert 'hello'.count('x', 0, 5) == 0

print("test_str_methods_extended passed")
