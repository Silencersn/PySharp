# Regression: zero-argument super() filled from the calling frame must
# follow CPython super_init_without_args - raise a catchable RuntimeError
# when the frame has no positional parameters or the first argument was
# deleted, and see through a MakeCell'd captured first parameter. Fails
# until the fix lands (Debug.Assert on slot 0 killed the process).


def plain():
    return super()


try:
    plain()
except RuntimeError as e:
    assert str(e) == "super(): no arguments", str(e)
else:
    raise AssertionError("expected RuntimeError")

try:
    super()
except RuntimeError as e:
    assert str(e) == "super(): no arguments", str(e)
else:
    raise AssertionError("expected RuntimeError")


def with_local():
    x = 1
    return super()


try:
    with_local()
except RuntimeError as e:
    assert str(e) == "super(): no arguments", str(e)
else:
    raise AssertionError("expected RuntimeError")


def with_local_after():
    super()
    x = 1


try:
    with_local_after()
except RuntimeError as e:
    assert str(e) == "super(): no arguments", str(e)
else:
    raise AssertionError("expected RuntimeError")


class NoArgMethod:
    def f():
        return super()


try:
    NoArgMethod.f()
except RuntimeError as e:
    assert str(e) == "super(): no arguments", str(e)
else:
    raise AssertionError("expected RuntimeError")


class DeletedSelf:
    def m(self):
        del self
        return super()


try:
    DeletedSelf().m()
except RuntimeError as e:
    assert str(e) == "super(): arg[0] deleted", str(e)
else:
    raise AssertionError("expected RuntimeError")


# a captured first parameter occupies a MakeCell'd slot
class Greets:
    def greet(self):
        return "greet"


class Greeter(Greets):
    def m(self):
        def inner():
            return self

        inner()
        return super().greet()


assert Greeter().m() == "greet"


# guards: the working forms stay working
class Base:
    def who(self):
        return "base"


class Mid(Base):
    def who(self):
        return "mid+" + super().who()


class Leaf(Mid):
    def who(self):
        return "leaf+" + super().who()


assert Leaf().who() == "leaf+mid+base"
assert super(Mid, Leaf()).who() == "base"


class CBase:
    @classmethod
    def make(cls):
        return cls.__name__


class CDer(CBase):
    @classmethod
    def make(cls):
        return "d<" + super().make() + ">"

assert CDer.make() == "d<CDer>"
