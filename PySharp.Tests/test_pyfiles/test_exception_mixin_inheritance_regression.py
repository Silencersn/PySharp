"""
Regression: exception classes combined with plain mixin bases (in either
base order) must stay fully functional exception classes.

CPython's best_base (Objects/typeobject.c) treats object's layout as the
zero-size root, so a plain mixin contributes no instance layout of its
own: class E(Mixin, Exception) and class E(Err, Mixin) both define,
instantiate, raise, and match except single-class, tuple and except*
handlers. Exception matching (PyErr_GivenExceptionMatches) walks the
full MRO, never a single first base, and stays consistent with
issubclass/isinstance. Only unrelated solid layouts such as Exception +
dict still raise "multiple bases have instance lay-out conflict", and a
non-exception except clause is still refused.
"""

class Mixin:
    pass

class MixinChain(Mixin):
    pass

class SlottedMixin:
    __slots__ = ()

class OtherMixin:
    pass

class DictMixin(dict):
    pass

def is_caught(handler, factory):
    try:
        raise factory()
    except handler:
        return True
    return False

# --- mixin first: define, construct, raise and match along the MRO ---
class E4(Mixin, Exception):
    pass

assert issubclass(E4, BaseException)
assert E4("boom").args == ("boom",)

exc = E4("x")
assert isinstance(exc, Exception) and isinstance(exc, Mixin)

assert is_caught(E4, lambda: E4("x"))
assert is_caught((E4, Exception), lambda: E4("x"))
assert is_caught(Exception, lambda: E4("x"))

raised_class = None
try:
    raise E4
except E4 as e:
    raised_class = e
assert isinstance(raised_class, E4)

# mixin before a builtin exception base, builtin and self handlers
class E9(Mixin, ValueError):
    pass

assert is_caught(ValueError, lambda: E9("x"))
assert is_caught(E9, lambda: E9("x"))

# --- exception first: mixed bases must not hit the layout conflict ---
class Err(Exception):
    pass

class E5(Err, MixinChain):
    pass

assert is_caught(Err, lambda: E5("x"))
assert is_caught(E5, lambda: E5("x"))

class E6(Exception, Mixin):
    pass

assert is_caught(E6, lambda: E6("x"))
assert is_caught(Exception, lambda: E6("x"))

# three bases with the exception in the middle, unrelated mixin last
class E3(Mixin, Err, OtherMixin):
    pass

assert is_caught(Exception, lambda: E3("x"))
assert is_caught(Err, lambda: E3("x"))

# __slots__ mixin and except* member matching
class ES(SlottedMixin, ValueError):
    pass

assert is_caught(ES, lambda: ES("x"))

star_caught = []
try:
    raise ExceptionGroup("eg", [E4("x")])
except* E4:
    star_caught.append(True)
assert star_caught == [True]

# --- guards: unrelated layouts still conflict, non-exceptions refused ---
for bases in ((Exception, dict), (dict, Exception), (DictMixin, Exception), (Exception, DictMixin)):
    try:
        type("X", bases, {})
        raise AssertionError(f"unrelated layouts must conflict: {bases}")
    except TypeError as e:
        assert str(e) == "multiple bases have instance lay-out conflict", str(e)

# a handler class outside the exception hierarchy is refused at match time
# (the TypeError escapes the try statement; later handlers cannot catch it)
try:
    try:
        raise ValueError("x")
    except Mixin:
        raise AssertionError("non-exception handler must be refused")
except TypeError as e:
    assert str(e) == "catching classes that do not inherit from BaseException is not allowed", str(e)

try:
    class EO(object, Exception):
        pass
    raise AssertionError("explicit object base must fail the MRO merge")
except TypeError as e:
    assert str(e) == "Cannot create a consistent method resolution order (MRO) for bases object, Exception", str(e)

print("exception mixin inheritance regression passed")
