"""__init_subclass__ rejections name the class being created, through super() forwards and type() alike.

:kind: test
"""

# Regression: the default hook's rejection message took its name from
# whatever class the classmethod descriptor happened to bind. Two
# binding paths drifted: a super() attribute fetch bound super's first
# argument instead of the object's own type (do_super_lookup), and the
# @deprecated class wrapper forwarded to an already-bound hook, naming
# the decorated base. type() call keywords also configured the class
# directly — CPython passes the whole kwds to __init_subclass__.


def show(label, f):
    try:
        r = f()
        print(label, "=", repr(r))
    except BaseException as e:
        print(label, "!", type(e).__name__, repr(str(e)))


def plain():
    class B:
        pass

    class Sub(B, extra=1):
        pass


def local_base():
    class B:
        pass

    def make():
        class Sub(B, extra=1):
            pass

    return make()


def forwarding():
    class Fwd:
        def __init_subclass__(cls, **kwargs):
            kwargs.pop("ok", None)
            super().__init_subclass__(**kwargs)

    def make():
        class SubFwd(Fwd, ok=1, extra=2):
            pass

    return make()


def explicit_bad_forward():
    class Fwd:
        def __init_subclass__(cls, **kwargs):
            bad = kwargs.pop("extra", None)
            if bad:
                super().__init_subclass__(extra=bad)
            else:
                super().__init_subclass__()

    def make():
        class SubFwd(Fwd, extra=2):
            pass

    return make()


show("plain", plain)
show("local_base", local_base)
show("forwarding", forwarding)
show("explicit_bad_forward", explicit_bad_forward)


class SomeBase:
    pass


show("direct_kw", lambda: SomeBase.__init_subclass__(mystery=1))
show("direct_pos", lambda: SomeBase.__init_subclass__(1))
show("object_pos", lambda: object.__init_subclass__(1))

cm = classmethod(lambda cls: ("bound", cls.__name__))


class CmBase:
    x = cm


class CmSub(CmBase):
    pass


print("super_cls_cm:", super(CmSub, CmSub).x)
print("super_obj_cm:", super(CmSub, CmSub()).x)
print("plain_cls_cm:", CmSub.x)

import warnings

show("type_qualname_kw", lambda: type("C", (), {}, __qualname__="Q", extra=1))


@warnings.deprecated("old")
class OldBase:
    pass


def deprecated_base():
    class Dep(OldBase, k=1):
        pass


show("deprecated_base", deprecated_base)


class Consumer:
    def __init_subclass__(cls, **kwargs):
        cls.captured = kwargs


consumed = type("C2", (Consumer,), {}, __qualname__="Q", tag=1)
print("consumed_kw:", consumed.captured, "| qualname:", consumed.__qualname__)
