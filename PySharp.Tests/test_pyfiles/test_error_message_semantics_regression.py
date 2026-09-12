"""
Regression: three error-message semantics fixes aligned with CPython:
- super attribute misses say "'super' object has no attribute 'name'"
- subscript stores/deletes on types without those slots say
  "does not support item assignment" / "doesn't support item deletion"
- deleting through a data descriptor without __delete__ raises a bare
  "__delete__" AttributeError, not a misleading "has no attribute"
"""

# super getattr failure message
class A:
    pass


class B(A):
    def m(self):
        return super().nope


try:
    B().m()
    raise AssertionError("super miss did not raise")
except AttributeError as e:
    assert str(e) == "'super' object has no attribute 'nope'", str(e)

# subscript assignment messages
try:
    (1, 2)[0] = 9
    raise AssertionError("tuple item assignment accepted")
except TypeError as e:
    assert str(e) == "'tuple' object does not support item assignment", str(e)

tpl = (1, 2)
try:
    tpl[0] += 1
    raise AssertionError("tuple augmented item assignment accepted")
except TypeError as e:
    assert str(e) == "'tuple' object does not support item assignment", str(e)

try:
    "ab"[0] = "x"
    raise AssertionError("str item assignment accepted")
except TypeError as e:
    assert str(e) == "'str' object does not support item assignment", str(e)


class Plain:
    pass


try:
    Plain()[0] = 1
    raise AssertionError("plain instance item assignment accepted")
except TypeError as e:
    assert str(e) == "'Plain' object does not support item assignment", str(e)

# valid stores keep working
lst = [1, 2]
lst[0] = 9
assert lst == [9, 2]

# subscript deletion messages
try:
    del tpl[0]
    raise AssertionError("tuple item deletion accepted")
except TypeError as e:
    assert str(e) == "'tuple' object doesn't support item deletion", str(e)

try:
    del Plain()[0]
    raise AssertionError("plain instance item deletion accepted")
except TypeError as e:
    assert str(e) == "'Plain' object doesn't support item deletion", str(e)

del lst[0]
assert lst == [2]

# deleting through a data descriptor without __delete__
class NoDel:
    def __get__(self, obj, objtype=None):
        return "get"

    def __set__(self, obj, v):
        pass


class C2:
    d = NoDel()


try:
    del C2().d
    raise AssertionError("data descriptor delete accepted")
except AttributeError as e:
    assert str(e) == "__delete__", str(e)

# non-data descriptors and plain class attributes keep the classic message
class Plain2:
    d = 1


try:
    del Plain2().d
    raise AssertionError("plain class attribute delete accepted")
except AttributeError as e:
    assert str(e) == "'Plain2' object has no attribute 'd'", str(e)


class OnlyGet:
    def __get__(self, obj, objtype=None):
        return "get"


class C3:
    d = OnlyGet()


try:
    del C3().d
    raise AssertionError("non-data descriptor delete accepted")
except AttributeError as e:
    assert str(e) == "'C3' object has no attribute 'd'", str(e)

# a custom __delete__ still runs and its error propagates
class WithDel:
    def __get__(self, o, t=None):
        return "g"

    def __set__(self, o, v):
        pass

    def __delete__(self, o):
        raise AttributeError("custom-del")


class C5:
    d = WithDel()


try:
    del C5().d
    raise AssertionError("custom __delete__ not invoked")
except AttributeError as e:
    assert str(e) == "custom-del", str(e)

# plain instance attributes delete normally
c = C2()
c.other = 1
del c.other
assert not hasattr(c, "other")

print("test_error_message_semantics_regression passed")
