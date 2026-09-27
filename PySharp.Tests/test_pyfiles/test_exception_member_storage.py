"""
built-in exception attributes live in C members, not in the instance __dict__.

CPython declares OSError/SyntaxError/ImportError/UnicodeError attributes in
PyMemberDef tables (Objects/exceptions.c OSError_members, SyntaxError_members,
ImportError_members, UnicodeError_members) and StopIteration/SystemExit's
value/code as struct slots. They are reached through a member descriptor and
never through tp_dict, so:

  - e.__dict__ stays empty of them (and of anything the constructor set);
  - an unset slot reads back as None;
  - a delete clears the slot and never raises (_Py_T_OBJECT, not
    _Py_T_OBJECT_EX: structmember.c PyMember_SetOne raises only for _EX),
    except a Py_T_PYSSIZET slot, which cannot be deleted at all;
  - a member write is not visible in __dict__, and a __dict__ write does not
    shadow the member because the descriptor is a data descriptor.

Only ExceptionGroup.message/exceptions are Py_READONLY and reject deletion.

:kind: test
"""

# --- the constructor-filled members never reach __dict__ ---------------------
assert OSError(2, "no", "p.txt").__dict__ == {}
assert SyntaxError("bad", ("f.py", 3, 10, "code")).__dict__ == {}
e = ImportError("x")
e.name = "n"
assert e.__dict__ == {}
assert UnicodeDecodeError("u", b"ab", 0, 1, "r").__dict__ == {}
assert UnicodeEncodeError("u", "ab", 0, 1, "r").__dict__ == {}
assert UnicodeTranslateError("ab", 0, 1, "r").__dict__ == {}

# the families whose storage is a dedicated slot were already correct
assert StopIteration(5).__dict__ == {}
assert SystemExit(3).__dict__ == {}
assert ExceptionGroup("m", [ValueError("a")]).__dict__ == {}

# --- a member write stays out of __dict__ ------------------------------------
o = OSError(2, "no")
o.errno = 7
assert o.errno == 7
assert o.__dict__ == {}

# --- a __dict__ write does not shadow the member -----------------------------
# the member descriptor is a data descriptor, so it wins over the instance dict
o = OSError(2, "no")
o.__dict__["errno"] = 99
assert o.errno == 2
assert o.__dict__ == {"errno": 99}

# the same for SyntaxError and ImportError
s = SyntaxError("m", ("f.py", 3, 10, "code"))
s.__dict__["lineno"] = 99
assert s.lineno == 3

i = ImportError("x")
i.__dict__["msg"] = "shadow"
assert i.msg == "x"
assert str(i) == "x"

# --- unset members read back as None -----------------------------------------
assert OSError(2, "no").filename is None
assert OSError(2, "no").filename2 is None
assert OSError(2, "no").winerror is None
assert ImportError("x").name is None
assert ImportError("x").path is None
assert ImportError("x").name_from is None
assert SyntaxError("m").lineno is None
assert SyntaxError("m").offset is None
assert SyntaxError("m").text is None
assert SyntaxError("m").end_lineno is None
assert SyntaxError("m").end_offset is None
assert SyntaxError("m")._metadata is None
assert StopIteration().value is None
assert SystemExit().code is None
# UnicodeTranslateError shares UnicodeError_members, so encoding exists unsent
assert UnicodeTranslateError("a", 0, 1, "r").encoding is None
assert UnicodeDecodeError("u", b"a", 0, 1, "r").start == 0

# --- a delete clears the slot and never raises -------------------------------
o = OSError(2, "no")
del o.errno
assert o.errno is None
assert hasattr(o, "errno")
assert o.__dict__ == {}
# a second delete is equally harmless
del o.errno
assert o.errno is None

o = OSError(2, "no", "p.txt")
del o.filename
assert o.filename is None
assert str(o) == "[Errno 2] no"

i = ImportError("x")
del i.msg
assert i.msg is None
assert str(i) == "x"

s = SyntaxError("m", ("f.py", 3, 10, "code"))
del s.lineno
assert s.lineno is None
assert str(s) == "m (f.py)"

s = SyntaxError("m", ("f.py", 3, 10, "code"))
del s.msg
assert s.msg is None
assert str(s) == "None (f.py, line 3)"

d = UnicodeDecodeError("u", b"ab", 0, 1, "r")
del d.reason
assert d.reason is None
assert hasattr(d, "reason")
assert d.__dict__ == {}

# deleting an already-unset member is fine too
i = ImportError()
del i.msg
assert i.msg is None

# --- the numeric members cannot be deleted -----------------------------------
for attr in ("start", "end"):
    d = UnicodeDecodeError("u", b"ab", 0, 1, "r")
    try:
        delattr(d, attr)
        assert False, "TypeError expected"
    except TypeError as err:
        assert str(err) == "can't delete numeric/char attribute", str(err)
    # the failed delete left the value alone
    assert getattr(d, attr) in (0, 1)

# --- ExceptionGroup's two members are read-only ------------------------------
g = ExceptionGroup("m", [ValueError("a")])
for attr in ("message", "exceptions"):
    try:
        delattr(g, attr)
        assert False, "AttributeError expected"
    except AttributeError as err:
        assert str(err) == "readonly attribute", str(err)

# --- assigning a numeric member goes through PyLong_AsSsize_t ----------------
class WithIndex:
    def __index__(self):
        return 7


d = UnicodeDecodeError("u", b"ab", 0, 1, "r")
# __init__ uses the "n" format, which consults __index__ ...
assert UnicodeDecodeError("u", b"ab", WithIndex(), 1, "r").start == 7
assert isinstance(UnicodeDecodeError("u", b"ab", WithIndex(), 1, "r").start, int)
try:
    UnicodeDecodeError("u", b"ab", 1.5, 1, "r")
    assert False, "TypeError expected"
except TypeError as err:
    assert str(err) == "'float' object cannot be interpreted as an integer", str(err)
try:
    UnicodeDecodeError("u", b"ab", 2 ** 70, 1, "r")
    assert False, "OverflowError expected"
except OverflowError as err:
    assert str(err) == "Python int too large to convert to C ssize_t", str(err)

# ... but the member setter does not, and reports the member's own wording
d = UnicodeDecodeError("u", b"ab", 0, 1, "r")
try:
    d.start = WithIndex()
    assert False, "TypeError expected"
except TypeError as err:
    assert str(err) == "an integer is required", str(err)
try:
    d.start = 2 ** 70
    assert False, "OverflowError expected"
except OverflowError as err:
    assert str(err) == "Python int too large to convert to C ssize_t", str(err)
try:
    d.start = "x"
    assert False, "TypeError expected"
except TypeError as err:
    assert str(err) == "an integer is required", str(err)
# a bool is an int, and the stored value is normalized to a plain int
d.start = True
assert d.start == 1
assert type(d.start) is int

# --- str() re-checks the object member at call time --------------------------
d = UnicodeDecodeError("u", b"ab", 0, 1, "r")
d.object = 5
try:
    str(d)
    assert False, "TypeError expected"
except TypeError as err:
    assert str(err) == "UnicodeError 'object' attribute must be a bytes", str(err)

e = UnicodeEncodeError("u", "ab", 0, 1, "r")
e.object = 5
try:
    str(e)
    assert False, "TypeError expected"
except TypeError as err:
    assert str(err) == "UnicodeError 'object' attribute must be a string", str(err)

# an uninitialized object slot renders as the empty string
assert str(UnicodeDecodeError.__new__(UnicodeDecodeError)) == ""

# --- str()/repr() and behaviour are otherwise untouched ----------------------
assert str(OSError(2, "no", "p.txt")) == "[Errno 2] no: 'p.txt'"
assert str(SyntaxError("bad", ("f.py", 3, 10, "code"))) == "bad (f.py, line 3)"
assert repr(OSError(2, "no")) == "FileNotFoundError(2, 'no')"
assert str(UnicodeDecodeError("u", b"ab", 0, 1, "r")) == \
    "'u' codec can't decode byte 0x61 in position 0: r"
assert str(UnicodeDecodeError("u", b"abcd", 1, 3, "r")) == \
    "'u' codec can't decode bytes in position 1-2: r"
assert str(UnicodeEncodeError("u", "ab", 0, 1, "r")) == \
    "'u' codec can't encode character '\\x61' in position 0: r"
assert str(UnicodeTranslateError("ab", 0, 1, "r")) == \
    "can't translate character '\\x61' in position 0: r"

# args keep the full tuple regardless of the members
assert SyntaxError("m", ("f.py", 3, 10, "code")).args == ("m", ("f.py", 3, 10, "code"))
assert UnicodeDecodeError("u", b"ab", 0, 1, "r").args == ("u", b"ab", 0, 1, "r")

# ImportError_str still prefers an exact str msg over the args rendering
assert str(ImportError("a", "b")) == "('a', 'b')"


class MyStr(str):
    pass


assert str(ImportError(MyStr("boom"))) == "boom"

# OSError still remaps a known errno to its subclass, members included
assert isinstance(OSError(2, "no"), FileNotFoundError)
assert FileNotFoundError(2, "no", "f.txt").__dict__ == {}

print("test_exception_member_storage passed")
