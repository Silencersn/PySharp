"""
Regression: a module attribute miss must produce a clean AttributeError
message - module 'sys' has no attribute 'nonexistent_zzz' - not leak the
internal object dump of the attribute name:

    module 'sys' has no attribute 'PyStrObject{id=1,repr='nonexistent_zzz'}'

The leak came from interpolating the PyObject item (PyStrObject.ToString)
into the message in PyModuleObject.GetAttr; all three miss paths leak it:
direct access, getattr(), and `from sys import missing`.

Instance and builtin-type attribute errors already produce clean messages
(pinned below as guards). The `from mod import missing` case currently
raises AttributeError; CPython raises ImportError there - the essential
pin is the clean message, so either type is accepted for that path.
"""

import sys


def err_of(fn):
    try:
        fn()
        raise AssertionError("expected an attribute/import error")
    except (AttributeError, ImportError) as e:
        return str(e)


def via_import():
    from sys import nonexistent_zzz


# red cases: the attribute name must appear as text, never as a dump
msg1 = err_of(lambda: sys.nonexistent_zzz)
assert "PyStrObject" not in msg1, msg1
assert "nonexistent_zzz" in msg1, msg1
assert "module 'sys'" in msg1, msg1

msg2 = err_of(lambda: getattr(sys, "nonexistent_zzz"))
assert "PyStrObject" not in msg2, msg2
assert "nonexistent_zzz" in msg2, msg2

msg3 = err_of(via_import)
assert "PyStrObject" not in msg3, msg3
assert "nonexistent_zzz" in msg3, msg3


# guards: non-module attribute misses are already clean
class C:
    pass


msg_g1 = err_of(lambda: C().nonexistent_zzz)
assert "PyStrObject" not in msg_g1 and "nonexistent_zzz" in msg_g1, msg_g1

msg_g2 = err_of(lambda: "x".nonexistent_zzz)
assert "PyStrObject" not in msg_g2 and "nonexistent_zzz" in msg_g2, msg_g2

msg_g3 = err_of(lambda: (1).nonexistent_zzz)
assert "PyStrObject" not in msg_g3 and "nonexistent_zzz" in msg_g3, msg_g3

print("test_module_attr_message_regression passed")
