"""
Regression: bool() with no argument returns False instead of raising
TypeError. CPython's bool is declared as bool(object=False, /), so the
zero-argument form is a legal call and bool_new starts from Py_False
(Objects/boolobject.c:29-42). The sibling zero-argument constructors
int()/float()/list()/str()/tuple()/dict()/set() were already correct and
stay correct.

CPython 3.14 reference (Objects/boolobject.c bool_new, _PyArg_NoKeywords,
PyArg_UnpackTuple with min=0 max=1).
"""


def msg(fn):
    try:
        return repr(fn())
    except TypeError as e:
        return "TypeError: " + str(e)
    except Exception as e:
        return type(e).__name__ + ": " + str(e)


# --- the fixed case: no argument at all is False ---

assert bool() is False
assert bool() == 0
assert isinstance(bool(), bool)
assert repr(bool()) == 'False'
assert str(bool()) == 'False'

# one argument still converts through truthiness
assert bool(0) is False
assert bool(1) is True
assert bool('') is False
assert bool('x') is True
assert bool([]) is False
assert bool([0]) is True
assert bool({}) is False
assert bool(None) is False
assert bool(0.0) is False
assert bool(2.5) is True

# --- arity and keyword rejection, in CPython's check order ---

# keywords are refused before the positional count is looked at
assert msg(lambda: bool(x=1)) == "TypeError: bool() takes no keyword arguments"
assert msg(lambda: bool(x=False)) == "TypeError: bool() takes no keyword arguments"
assert msg(lambda: bool(object=False)) == "TypeError: bool() takes no keyword arguments"
assert msg(lambda: bool(**{'x': 1})) == "TypeError: bool() takes no keyword arguments"
assert msg(lambda: bool(1, x=1)) == "TypeError: bool() takes no keyword arguments"
assert msg(lambda: bool(1, 2, x=1)) == "TypeError: bool() takes no keyword arguments"

# too many positionals
assert msg(lambda: bool(1, 2)) == "TypeError: bool expected at most 1 argument, got 2"
assert msg(lambda: bool(1, 2, 3)) == "TypeError: bool expected at most 1 argument, got 3"

# --- controls: the sibling zero-argument constructors still work ---

assert int() == 0
assert float() == 0.0
assert list() == []
assert str() == ''
assert tuple() == ()
assert dict() == {}
assert set() == set()
assert complex() == complex()
assert bytearray() == bytearray()

# --- truthiness still routes through __bool__ / __len__ ---

class Truthy:
    def __bool__(self):
        return True

class Falsy:
    def __bool__(self):
        return False

class LenZero:
    def __len__(self):
        return 0

class LenThree:
    def __len__(self):
        return 3

class BadBool:
    def __bool__(self):
        return 1

assert bool(Truthy()) is True
assert bool(Falsy()) is False
assert bool(LenZero()) is False
assert bool(LenThree()) is True
# CPython names the *returned* type, not the receiver's
assert msg(lambda: bool(BadBool())) == "TypeError: __bool__ should return bool, returned int"

# bool is a subclass of int and cannot be subclassed
assert issubclass(bool, int) is True
assert bool() + 1 == 1
assert bool(1) + 1 == 2
assert msg(lambda: type('B', (bool,), {})) == "TypeError: type 'bool' is not an acceptable base type"

print("bool no-arg regression passed")
