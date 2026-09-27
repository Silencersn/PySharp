"""super(type, obj) names both sides of the failed check and picks the wording by whether obj is a type.

The message is "super(type, obj): obj (<type|instance of> <obj name>) is not an
instance or subtype of type (<type name>)." The obj name is its tp_name, and
both names are cut to 200 UTF-8 bytes.

:kind: test
:background: supercheck in Objects/typeobject.c names the two types and
    branches on PyType_Check(obj) for the "type" / "instance of" wording; the
    %.200s precision on both names counts UTF-8 bytes, so a cut landing inside
    a multi-byte sequence backs off to the previous character boundary.
"""


def err_of(fn):
    try:
        fn()
    except TypeError as e:
        return str(e)
    raise AssertionError("expected TypeError")


class Unrelated:
    pass


class Unrelated2:
    pass


class Outer:
    class Inner:
        pass


# "instance of <name>" when obj is not itself a type
assert err_of(lambda: super(Unrelated, Unrelated2())) == (
    "super(type, obj): obj (instance of Unrelated2) is not an instance or "
    "subtype of type (Unrelated)."
)

# "type <name>" when obj is a class, whatever the class
assert err_of(lambda: super(Unrelated, Unrelated2)) == (
    "super(type, obj): obj (type Unrelated2) is not an instance or subtype "
    "of type (Unrelated)."
)

# builtin types go through the same two branches
assert err_of(lambda: super(Unrelated, 5)) == (
    "super(type, obj): obj (instance of int) is not an instance or subtype "
    "of type (Unrelated)."
)
assert err_of(lambda: super(Unrelated, int)) == (
    "super(type, obj): obj (type int) is not an instance or subtype of "
    "type (Unrelated)."
)

# the name is tp_name, which for a class created at runtime is the bare
# __name__ — a nested class reports "Inner", not "Outer.Inner"
assert err_of(lambda: super(Unrelated, Outer.Inner)) == (
    "super(type, obj): obj (type Inner) is not an instance or subtype of "
    "type (Unrelated)."
)
assert err_of(lambda: super(Unrelated, Outer.Inner())) == (
    "super(type, obj): obj (instance of Inner) is not an instance or subtype "
    "of type (Unrelated)."
)

# a name at the cap passes through whole; an overlong one is cut to 200 bytes
for length, expected in ((200, 200), (260, 200)):
    long_name = "L" * length
    message = err_of(lambda: super(Unrelated, type(long_name, (), {})))
    assert message == (
        "super(type, obj): obj (type %s) is not an instance or subtype of "
        "type (Unrelated)." % ("L" * expected)
    ), (length, len(message))

# the cut counts UTF-8 bytes, not characters: 70 three-byte characters would
# fill 210 bytes, so the name stops at 66 (198 bytes) without splitting one
cjk_message = err_of(lambda: super(Unrelated, type("\u4e2d" * 70, (), {})))
assert cjk_message == (
    "super(type, obj): obj (type %s) is not an instance or subtype of "
    "type (Unrelated)." % ("\u4e2d" * 66)
), len(cjk_message)

# guards: the accepted forms stay accepted and keep their wording
assert super(Unrelated, Unrelated) is not None
assert super(Unrelated, Unrelated()) is not None
assert super(Unrelated2, Unrelated2) is not None

print("test_super_obj_not_match_type_message passed")
