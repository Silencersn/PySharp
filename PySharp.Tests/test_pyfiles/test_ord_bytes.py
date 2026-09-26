"""Verifies ord() accepts one-byte bytes and bytearray values (and bytes subclasses) and matches CPython's wrong-length and wrong-type error messages.

:kind: test
"""

# ord() behavior: CPython builtin_ord accepts one-byte bytes and
# bytearray objects (returning the byte value), all three accepted types
# share the "expected a character, but string of length N found"
# wrong-length message, only other types get the type-name message, and
# bytes subclasses are accepted through the bytes branch.

assert ord(b'A') == 65
assert ord(bytearray(b'B')) == 66
assert ord(b'\x00') == 0
assert ord(bytearray(b'\xff')) == 255

assert ord('A') == 65
assert ord('λ') == 955
assert ord('😀') == 128512


def error_of(value):
    try:
        ord(value)
    except TypeError as e:
        return str(e)
    raise AssertionError("ord() must reject " + repr(value))


assert error_of(b'ab') == "ord() expected a character, but string of length 2 found"
assert error_of(bytearray(b'')) == "ord() expected a character, but string of length 0 found"
assert error_of('ab') == "ord() expected a character, but string of length 2 found"
assert error_of('') == "ord() expected a character, but string of length 0 found"

assert error_of(65) == "ord() expected string of length 1, but int found"
assert error_of(True) == "ord() expected string of length 1, but bool found"
assert error_of(memoryview(b'A')) == "ord() expected string of length 1, but memoryview found"


class B(bytes):
    pass


assert ord(B(b'A')) == 65

print("ok")
