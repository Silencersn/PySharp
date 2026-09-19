# Regression: dedicated __str__ and members for the OSError,
# UnicodeEncodeError / UnicodeTranslateError and SyntaxError builtins.
#
# Objects/exceptions.c: OSError_str renders [Errno %S] %S with the
# : %R / -> %R filename suffixes and the MS_WINDOWS [WinError %S]
# branches; OSError_new swaps the allocated type for a mapped errno and
# oserror_init truncates args to (errno, strerror) once a filename is
# stored. UnicodeEncodeError_str / UnicodeTranslateError_str escape a
# single bad code point by magnitude (\x, \u, \U). SyntaxError_str
# renders "msg (basename, line N)" over the info tuple, and its member
# table defaults to None.

# --- OSError str forms ---
assert str(OSError(2, 'no file')) == "[Errno 2] no file"
assert str(OSError(2, 'no file', 'path.txt')) == "[Errno 2] no file: 'path.txt'"
assert str(OSError(2, 'no file', 'path.txt', None)) == "[WinError None] no file: 'path.txt'"
assert str(OSError(2, 'no file', 'path.txt', None, 'caller')) == "[WinError None] no file: 'path.txt' -> 'caller'"
assert str(OSError(2)) == "2"                       # 1-arg: plain args str
assert str(OSError(None, 'x')) == "[Errno None] x"
assert str(OSError(2, 'no file', 'p', 87)) == "[WinError 87] no file: 'p'"
assert str(OSError(2, 'no file', 'p', 87, 'f2')) == "[WinError 87] no file: 'p' -> 'f2'"

# --- errno -> subclass mapping (exact OSError calls only) ---
assert type(OSError(2, 'no file')) is FileNotFoundError
assert type(OSError(13, 'p')) is PermissionError
assert type(OSError(1, 'p')) is PermissionError
assert type(OSError(999999, 'x')) is OSError
assert type(OSError(2)) is OSError                  # 1-arg: no mapping
assert type(FileNotFoundError(2, 'x')) is FileNotFoundError
assert repr(OSError(2, 'no file')) == "FileNotFoundError(2, 'no file')"

# --- OSError members and args truncation ---
oe = OSError(2, 'no file', 'path.txt')
assert (oe.errno, oe.strerror, oe.filename, oe.filename2) == (2, 'no file', 'path.txt', None)
assert oe.args == (2, 'no file'), repr(oe.args)     # truncated for legacy unpacking

oe2 = OSError(2, 'x')
assert (oe2.errno, oe2.strerror, oe2.filename) == (2, 'x', None)
assert oe2.args == (2, 'x')

oe3 = OSError(2, 'x', None)                         # None filename: no truncation
assert oe3.args == (2, 'x', None) and oe3.filename is None

# live member writes keep __str__ current
oe2.errno = 7
oe2.strerror = 'y'
assert str(oe2) == "[Errno 7] y", str(oe2)

try:
    OSError(1, 'a', filename='f')
    assert False, "OSError takes no keyword arguments"
except TypeError as ex:
    assert str(ex) == "OSError() takes no keyword arguments", str(ex)

# --- UnicodeEncodeError ---
ue = UnicodeEncodeError('ascii', 'a', 0, 1, 'ordinal not in range')
assert str(ue) == "'ascii' codec can't encode character '\\x61' in position 0: ordinal not in range", str(ue)
assert (ue.encoding, ue.object, ue.start, ue.end, ue.reason) == ('ascii', 'a', 0, 1, 'ordinal not in range')

assert str(UnicodeEncodeError('ascii', 'a\u3042\U0001F600b', 2, 3, 'r')) == \
    "'ascii' codec can't encode character '\\U0001f600' in position 2: r"
assert str(UnicodeEncodeError('ascii', 'a\u3042b', 1, 3, 'r')) == \
    "'ascii' codec can't encode characters in position 1-2: r"
assert str(UnicodeEncodeError('ascii', 'a', 5, 6, 'r')) == \
    "'ascii' codec can't encode characters in position 5-5: r"
assert str(UnicodeEncodeError('ascii', '\ud800', 0, 1, 'surrogate')) == \
    "'ascii' codec can't encode character '\\ud800' in position 0: surrogate"

try:
    UnicodeEncodeError('ascii', 'a', 0, 1)
    assert False, "UnicodeEncodeError takes exactly 5 arguments"
except TypeError as ex:
    assert str(ex) == "function takes exactly 5 arguments (4 given)", str(ex)
try:
    UnicodeEncodeError('ascii', b'b', 0, 1, 'r')
    assert False, "object must be str"
except TypeError as ex:
    assert str(ex) == "argument 2 must be str, not bytes", str(ex)
try:
    UnicodeEncodeError('ascii', 'a', 'x', 1, 'r')
    assert False, "start must be an integer"
except TypeError as ex:
    assert str(ex) == "'str' object cannot be interpreted as an integer", str(ex)

# --- UnicodeTranslateError ---
assert str(UnicodeTranslateError('a\u3042b', 1, 2, 'untranslate')) == \
    "can't translate character '\\u3042' in position 1: untranslate"
assert str(UnicodeTranslateError('a\u3042b', 1, 3, 'r')) == \
    "can't translate characters in position 1-2: r"

te = UnicodeTranslateError('ab', 0, 1, 'r')
assert (te.object, te.start, te.end, te.reason) == ('ab', 0, 1, 'r')

try:
    UnicodeTranslateError('ab', 0, 1)
    assert False, "UnicodeTranslateError takes exactly 4 arguments"
except TypeError as ex:
    assert str(ex) == "function takes exactly 4 arguments (3 given)", str(ex)

# --- UnicodeDecodeError (messages aligned with the 3.14 getargs style,
# and memoryview objects accepted like any buffer) ---
assert str(UnicodeDecodeError('utf-8', b'a\xffb', 1, 2, 'invalid start byte')) == \
    "'utf-8' codec can't decode byte 0xff in position 1: invalid start byte"
assert str(UnicodeDecodeError('utf-8', memoryview(b'a\xff'), 1, 2, 'r')) == \
    "'utf-8' codec can't decode byte 0xff in position 1: r"
try:
    UnicodeDecodeError('utf-8', 'a', 0, 1, 'r')
    assert False, "object must be a bytes-like object"
except TypeError as ex:
    assert str(ex) == "a bytes-like object is required, not 'str'", str(ex)

# --- SyntaxError ---
se = SyntaxError('bad', ('f.py', 3, 10, 'code'))
assert str(se) == 'bad (f.py, line 3)', str(se)
assert (se.msg, se.filename, se.lineno, se.offset, se.text) == ('bad', 'f.py', 3, 10, 'code')
assert (se.end_lineno, se.end_offset, se.print_file_and_line) == (None, None, None)
assert se.args == ('bad', ('f.py', 3, 10, 'code')), repr(se.args)

assert str(SyntaxError('bad')) == 'bad'
assert str(SyntaxError()) == 'None'
assert str(SyntaxError('bad', ('f.py', None, None, None))) == 'bad (f.py)'
assert str(SyntaxError('bad', (None, 3, None, None))) == 'bad (line 3)'
assert str(SyntaxError('bad', ('f.py', '3', None, None))) == 'bad (f.py)'    # non-int lineno
assert str(SyntaxError('bad', ['f.py', 3, 10, 'code'])) == 'bad (f.py, line 3)'  # any sequence
assert str(SyntaxError('bad', ('dir\\f.py', 3, None, None))) == 'bad (f.py, line 3)'  # basename

se.msg = 'other'
assert str(se) == 'other (f.py, line 3)', str(se)

try:
    SyntaxError('bad', None)
    assert False, "info must be iterable"
except TypeError as ex:
    assert str(ex) == "'NoneType' object is not iterable", str(ex)
try:
    SyntaxError('bad', ('f', 3, 10, 'c', 4))
    assert False, "end_offset required with end_lineno"
except TypeError as ex:
    assert str(ex) == "end_offset must be provided when end_lineno is provided", str(ex)

se7 = SyntaxError('bad', ('f', 3, 10, 'c', 4, 5, 'meta'))
assert (se7.end_lineno, se7.end_offset, se7._metadata) == (4, 5, 'meta')

print("ok")
