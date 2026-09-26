"""Verifies the std stream type names itself consistently: __module__, __name__, __qualname__ and the two reprs all agree on one name, and the error messages name that same type.

:kind: test
:background: The stdio type once carried a module prefix in its __qualname__ while __module__ said builtins, so the type repr printed the bare name and the instance repr printed the prefixed one. CPython's io stack types (Objects/typeobject.c object_repr/type_repr, Objects/object.c for the tp_name in messages) keep a single name across all four.
"""

# The identity contract is internal consistency, so it is stated in terms of
# whatever name the implementation uses: each surface must agree with the
# others. The self-contradicting shape this pins down is a bare type repr
# next to a module-prefixed instance repr.
import sys

for stream in (sys.stdin, sys.stdout, sys.stderr):
    cls = type(stream)
    module = cls.__module__
    name = cls.__name__

    # a type outside builtins renders module.qualname in both reprs, and
    # __qualname__ carries no module prefix of its own
    assert module == "_io", module
    assert cls.__qualname__ == name, cls.__qualname__
    assert repr(cls) == "<class '%s.%s'>" % (module, name), repr(cls)
    assert repr(stream).startswith("<%s.%s " % (module, name)), repr(stream)

    # the type name in a message is the same name the reprs show
    try:
        stream[0]
    except TypeError as e:
        assert name in str(e), str(e)
    else:
        raise AssertionError("expected TypeError")

# all three streams share one type
assert type(sys.stdin) is type(sys.stdout) is type(sys.stderr)

print("stdio type identity ok")
