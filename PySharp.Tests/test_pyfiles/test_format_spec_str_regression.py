"""
Regression: format specs on str values must implement the CPython mini
language (Python/formatter_unicode.c: format_string_internal), and
str.format must parse the full field grammar
(Objects/stringlib/unicode_format.h: parse_field / get_field_object).

PySharp used to have no str.__format__ at all - every non-empty spec
raised "unsupported format string passed to str.__format__" (the
object.__format__ default), 'ab'.__format__ raised AttributeError, and
str.format's hand-rolled field parser rejected any spec-bearing field
with an empty-key KeyError, never parsed !r/!s/!a conversions, did not
resolve nested spec fields, and had no [index]/.attr access.
"""


def error_of(exc, fn):
    try:
        fn()
    except exc as e:
        return str(e)
    raise AssertionError(f"{exc.__name__} not raised")


# str.__format__ rendering: align / fill / width / precision / 's' type
assert format('ab', '') == 'ab'
assert format('ab', 's') == 'ab'
assert format('ab', '10') == 'ab        '
assert format('ab', '<10') == 'ab        '
assert format('ab', '>10') == '        ab'
assert format('ab', '^10') == '    ab    '
assert format('ab', '*^10') == '****ab****'
assert format('ab', '.1') == 'a'
assert format('ab', '10.1') == 'a         '
assert format('ab', '.0') == ''
assert format('ab', '5.6') == 'ab   '
assert format('ab', '05') == 'ab000'
assert format('ab', '010') == 'ab00000000'
assert format('ab', '^06') == '00ab00'
assert format('ab', '10.1s') == 'a         '
assert format('ab', '*<5') == 'ab***'

# invalid specs: exact CPython error forms and precedence
assert error_of(ValueError, lambda: format('ab', 'd')) == "Unknown format code 'd' for object of type 'str'"
assert error_of(ValueError, lambda: format('ab', 'q')) == "Unknown format code 'q' for object of type 'str'"
assert error_of(ValueError, lambda: format('ab', '+5')) == 'Sign not allowed in string format specifier'
assert error_of(ValueError, lambda: format('ab', ' 5')) == 'Space not allowed in string format specifier'
assert error_of(ValueError, lambda: format('ab', '#5')) == 'Alternate form (#) not allowed in string format specifier'
assert error_of(ValueError, lambda: format('ab', '=5')) == "'=' alignment not allowed in string format specifier"
assert error_of(ValueError, lambda: format('ab', 'z5')) == 'Negative zero coercion (z) not allowed in string format specifier'
assert error_of(ValueError, lambda: format('ab', ',')) == "Cannot specify ',' with 's'."
assert error_of(ValueError, lambda: format('ab', ',5')) == "Cannot specify ',' with '5'."
assert error_of(ValueError, lambda: format('ab', '+d')) == "Unknown format code 'd' for object of type 'str'"
assert error_of(ValueError, lambda: format('ab', ',d')) == "Unknown format code 'd' for object of type 'str'"
assert error_of(ValueError, lambda: format('ab', 's5')) == "Invalid format specifier 's5' for object of type 'str'"

# the dunder itself must exist and dispatch
assert 'ab'.__format__('>6') == '    ab'

# the same rendering through f-strings (nested specs included)
assert f"{'ab':>10}" == '        ab'
assert f"{'ab'!r:>{10}}" == "      'ab'"
width = 8
assert f"{'hi':*^{width}}" == '***hi***'
assert f"{3.14159:.{2}f}" == '3.14'

# str.format: spec-bearing fields, auto and manual numbering
assert '{:>10}'.format('ab') == '        ab'
assert '{:!>6}'.format('x') == '!!!!!x'
assert '{0:>10}'.format('ab') == '        ab'
assert '{a:^7}'.format(a='hi') == '  hi   '
assert '{:.>6}'.format('ab') == '....ab'

# conversions
assert '{0!r}'.format('x') == "'x'"
assert '{!r}'.format('txt') == "'txt'"
assert '{0!s}'.format('x') == 'x'
assert '{0!a}'.format('x') == "'x'"
assert '{0!a}'.format('é') == "'\\xe9'"
assert '{0!r:>8}'.format('x') == "     'x'"

# field access
assert '{0[1]}'.format([1, 2, 3]) == '2'
assert '{d[k]}'.format(d={'k': 9}) == '9'

# nested spec fields, one level deep
assert '{0:>{1}}'.format('ab', 10) == '        ab'
assert '{:{}}'.format(3.14159, '.2f') == '3.14'
assert '{0:{1}}'.format('abc', 's') == 'abc'
assert '{:{}.{}f}'.format(3.14159, 10, 2) == '      3.14'

# brace escapes and malformed templates
assert '{{}}'.format() == '{}'
assert 'a}}b'.format() == 'a}b'
assert 'a{{b'.format() == 'a{b'
assert error_of(ValueError, lambda: '{:'.format(1)) == "unmatched '{' in format spec"
assert error_of(ValueError, lambda: '{0!'.format(1)) == 'end of string while looking for conversion specifier'
assert error_of(ValueError, lambda: '{0!x}'.format(1)) == 'Unknown conversion specifier x'
assert error_of(ValueError, lambda: '{'.format()) == "Single '{' encountered in format string"
assert error_of(ValueError, lambda: '}'.format()) == "Single '}' encountered in format string"
assert error_of(ValueError, lambda: '{0'.format()) == "expected '}' before end of string"
assert error_of(ValueError, lambda: '{0.}'.format(1)) == 'Empty attribute in format string'
assert error_of(ValueError, lambda: '{0[0]x}'.format([1])) == "Only '.' or '[' may follow ']' in format field specifier"
assert error_of(ValueError, lambda: '{0:{1:{2}}}'.format(1, 'd', '')) == 'Max string recursion exceeded'
error_of(KeyError, lambda: '{q}'.format())
error_of(IndexError, lambda: '{1}'.format('a'))

# user-defined __format__ is reachable from both str.format and format()
class C:
    def __format__(self, spec):
        return 'F<' + spec + '>'


assert '{:abc}'.format(C()) == 'F<abc>'
assert format(C(), 'x') == 'F<x>'

# non-ASCII truncation counts code points, not UTF-16 units
assert f"{'héllo':.3}" == 'hél'
