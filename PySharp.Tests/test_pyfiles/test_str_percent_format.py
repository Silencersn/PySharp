"""str % formatting follows CPython's argument ordering and conversion rules, raising CPython's exact TypeErrors.

'*' consumes width/precision before the value, mapping keys %(name) drive the
mapping lookup, %d/%i/%u accept numbers with floats truncating toward zero
while %x/%X/%o require __index__, and the '0' flag pads string conversions
with spaces instead of zeros.

:kind: test
"""

# str % formatting: '*' takes width/precision from the argument tuple
# BEFORE the value itself (CPython ordering), mapping keys %(name) drive
# the mapping lookup (a plain dict without keys formats as a single
# value), %d/%i/%u accept any number with float truncating toward zero
# while %x/%X/%O require __index__, and the per-type TypeErrors match
# the CPython messages.
def expect(exc_type, msg, fn):
    try:
        fn()
    except exc_type as e:
        assert str(e) == msg, str(e)
        return
    raise AssertionError("expected " + exc_type.__name__ + ": " + msg)

# --- * width/precision: argument order ---
assert "%*d" % (5, 42) == "   42"
assert "%-*d" % (5, 42) == "42   "
assert "%.*f" % (2, 3.14159) == "3.14"
assert "%*s" % (4, "ab") == "  ab"
assert "%*.*f" % (10, 3, 3.14159) == "     3.142"
assert "%0*d" % (6, -42) == "-00042"
assert "%*d" % (-5, 42) == "42   "          # negative width -> left align
assert "%.*s" % (2, "abcdef") == "ab"
assert "%.*d" % (5, 42) == "00042"
assert "%*c" % (5, 65) == "    A"
assert "%*d" % (True, 42) == "42"           # bool counts as an int width

expect(TypeError, "* wants int", lambda: "%*d" % ('5', 42))
expect(TypeError, "* wants int", lambda: "%.*f" % (2.5, 3.14))
expect(TypeError, "not enough arguments for format string", lambda: "%*d" % (5,))
expect(TypeError, "not all arguments converted during string formatting", lambda: "%*d" % (5, 42, 99))
expect(TypeError, "not all arguments converted during string formatting", lambda: "%d %d" % (5, 42, 99))
expect(TypeError, "not enough arguments for format string", lambda: "%(a)*d" % {'a': 1})

# --- mapping keys drive the mapping lookup ---
d = {'a': 1}
assert '%(a)s' % d == '1'
assert '%(a)s %(b)s' % {'a': 1, 'b': 2} == '1 2'
assert '%(a(b)c)s' % {'a(b)c': 7} == '7'

assert '%s' % d == "{'a': 1}"               # dict is a plain single value
assert '%s' % {} == '{}'
assert '%s' % ({'a': 1},) == "{'a': 1}"     # dict inside a tuple unpacks
assert 'abc' % {} == 'abc'                  # no markers: no leftover check
assert '%s' % 5 == '5'
assert '%s' % [1] == '[1]'

expect(TypeError, "not enough arguments for format string", lambda: '%s %s' % d)
expect(TypeError, "format requires a mapping", lambda: '%(a)s' % 5)
expect(TypeError, "format requires a mapping", lambda: '%(a)s' % (1,))
expect(TypeError, "format requires a mapping", lambda: '%(a)s' % 'abc')
expect(TypeError, "format requires a mapping", lambda: '%(x)s' % ())
expect(TypeError, "not all arguments converted during string formatting", lambda: 'abc' % 5)

# --- %d/%i/%u accept numbers, float truncates toward zero ---
assert '%d' % 3.9 == '3'
assert '%d' % -3.9 == '-3'
assert '%d' % 3.0 == '3'
assert '%i' % 2.7 == '2'
assert '%u' % -2.7 == '-2'
assert '%(k)d' % {'k': 3.9} == '3'
assert '%d' % True == '1'

class MyInt:
    def __int__(self):
        return 7

class MyIdx:
    def __index__(self):
        return 9

assert '%d' % MyInt() == '7'
assert '%d' % MyIdx() == '9'
assert '%x' % MyIdx() == '9'

# --- %x/%X/%o require an integer (__index__), non-ints get the
# conversion-specific TypeError ---
assert '%x' % 255 == 'ff'
expect(TypeError, "%x format: an integer is required, not float", lambda: '%x' % 3.9)
expect(TypeError, "%X format: an integer is required, not float", lambda: '%X' % 3.9)
expect(TypeError, "%o format: an integer is required, not float", lambda: '%o' % 3.9)
expect(TypeError, "%x format: an integer is required, not str", lambda: '%x' % 'ff')
expect(TypeError, "%x format: an integer is required, not MyInt", lambda: '%x' % MyInt())
expect(TypeError, "%d format: a real number is required, not str", lambda: '%d' % '5')
expect(TypeError, "%d format: a real number is required, not NoneType", lambda: '%d' % None)
expect(TypeError, "%d format: a real number is required, not dict", lambda: '%d' % d)

# --- key parsing ---
expect(ValueError, "incomplete format key", lambda: '%(abc' % {})
assert '%.*d' % (-1, 42) == '42'            # negative dynamic precision clamps

# --- '0' flag pads string-like conversions with spaces, not zeros
# (CPython gates F_ZERO on arg->sign, which s/r/a/c never set) ---
assert '%05s' % 'ab' == '   ab'
assert '%05.5s' % 'ab' == '   ab'
assert '%05.2s' % 'abcdef' == '   ab'
assert '%+05s' % 'ab' == '   ab'
assert '% 05s' % 'ab' == '   ab'
assert '%05s' % '' == '     '
assert '%05r' % 'ab' == " 'ab'"
assert '%05a' % 'ab' == " 'ab'"
assert '%05c' % 65 == '    A'
assert '%05c' % 'A' == '    A'
assert '%-05s' % 'ab' == 'ab   '
# numeric conversions keep zero-fill
assert '%05d' % 12 == '00012'
assert '%#05x' % 255 == '0x0ff'
assert '%+05d' % 12 == '+0012'
