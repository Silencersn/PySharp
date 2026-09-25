"""
Regression: the TypeError raised when isinstance/issubclass get a second
argument that is neither a type nor a tuple names the PEP 604 union as a
third accepted form, and issubclass uses CPython's "class" wording rather
than reusing isinstance's "type" wording. Both paths are hardcoded
separately in Python/bltinmodule.c, so the two messages are not one shared
template. Only the text changes — which inputs are accepted is unchanged.

CPython 3.14 reference (Python/bltinmodule.c builtin_isinstance_impl,
builtin_issubclass_impl).
"""

ISINSTANCE_MSG = "isinstance() arg 2 must be a type, a tuple of types, or a union"
ISSUBCLASS_MSG = "issubclass() arg 2 must be a class, a tuple of classes, or a union"
ISSUBCLASS_ARG1_MSG = "issubclass() arg 1 must be a class"


def msg(fn):
    try:
        return repr(fn())
    except TypeError as e:
        return "TypeError: " + str(e)
    except Exception as e:
        return type(e).__name__ + ": " + str(e)


# --- the two rejection messages, each with its own wording ---

assert msg(lambda: isinstance(1, 5)) == "TypeError: " + ISINSTANCE_MSG
assert msg(lambda: issubclass(int, 5)) == "TypeError: " + ISSUBCLASS_MSG

# every non-type, non-tuple classinfo reaches the same message
for bad in (5, 5.0, 'int', None, [int], {int}, b'int', bytearray(b'int'), range(1), True):
    assert msg(lambda v=bad: isinstance(1, v)) == "TypeError: " + ISINSTANCE_MSG, repr(bad)
    assert msg(lambda v=bad: issubclass(int, v)) == "TypeError: " + ISSUBCLASS_MSG, repr(bad)

# a classinfo that is neither accepted nor a container of one
class Plain:
    pass

assert msg(lambda: isinstance(1, Plain())) == "TypeError: " + ISINSTANCE_MSG
assert msg(lambda: issubclass(int, Plain())) == "TypeError: " + ISSUBCLASS_MSG

# --- arg 1 keeps its own message and is unchanged ---

assert msg(lambda: issubclass(1, int)) == "TypeError: " + ISSUBCLASS_ARG1_MSG
assert msg(lambda: issubclass(1, 5)) == "TypeError: " + ISSUBCLASS_ARG1_MSG
# isinstance imposes no constraint on arg 1 at all: the int that issubclass
# rejects as arg 1 is fine here
assert isinstance(1, int) is True
assert isinstance(None, int) is False
assert isinstance(5, str) is False
assert isinstance("s", type) is False

# --- controls: accepted forms still behave as before ---

assert isinstance(1, int) is True
assert isinstance(1, str) is False
assert issubclass(int, int) is True
assert issubclass(bool, int) is True
assert issubclass(int, bool) is False

# a tuple accepts non-type members it never has to look at
assert isinstance(1, (int, 5)) is True
assert issubclass(int, (int, 5)) is True
assert isinstance(1, (str, float)) is False
assert issubclass(int, (str, float)) is False
# nested tuples recurse
assert isinstance(1, (str, (int, 5))) is True
assert issubclass(int, (str, (int, 5))) is True
assert isinstance(1, ((int,),)) is True
assert issubclass(int, ((int,),)) is True
# an empty tuple is False on both sides
assert isinstance(1, ()) is False
assert issubclass(int, ()) is False

# a tuple that is itself the bad member, one level down
assert msg(lambda: isinstance(1, (str, (float, 5)))) == "TypeError: " + ISINSTANCE_MSG
assert msg(lambda: issubclass(int, (str, (float, 5)))) == "TypeError: " + ISSUBCLASS_MSG

# --- the __instancecheck__ / __subclasscheck__ hooks still win ---

class Meta(type):
    def __instancecheck__(cls, inst):
        return inst == "yes"

    def __subclasscheck__(cls, sub):
        return sub is int


class Check(metaclass=Meta):
    pass

assert isinstance("yes", Check) is True
assert isinstance("no", Check) is False
assert issubclass(int, Check) is True
assert issubclass(str, Check) is False

print("isinstance/issubclass arg 2 message regression passed")
