"""Verifies a __hash__ returning a non-int is rejected with CPython's slot_tp_hash sentence rather than the shared non-<type> template, including through the dict-key and set-element wrappings, while __index__/__int__ keep the template they really use.

:kind: test
"""

HASH_MSG = "__hash__ method should return an integer"


def expect(label, fn, msg):
    try:
        fn()
    except TypeError as e:
        assert str(e) == msg, f"{label}: {str(e)!r} != {msg!r}"
    else:
        assert False, f"{label}: no TypeError"


def hashable(x):
    hash(x)


class S:
    def __hash__(self):
        return "x"


class N:
    def __hash__(self):
        return None


class F:
    def __hash__(self):
        return 1.5


class T:
    def __hash__(self):
        return (1,)


class L:
    def __hash__(self):
        return [1]


# the message names no return type at all — every wrong type collapses onto
# the same sentence
for label, cls in [("str", S), ("None", N), ("float", F),
                   ("tuple", T), ("list", L)]:
    expect(f"hash({label})", lambda c=cls: hash(c()), HASH_MSG)

# bool is an int subclass, so it is accepted rather than rejected
class B:
    def __hash__(self):
        return True


assert hash(B()) == 1

# the dict-key and set-element wrappings embed the same sentence
expect("dict key", lambda: {}.__setitem__(S(), 0),
       f"cannot use 'S' as a dict key ({HASH_MSG})")
expect("set add", lambda: set().add(S()),
       f"cannot use 'S' as a set element ({HASH_MSG})")
expect("dict literal", lambda: {S(): 1},
       f"cannot use 'S' as a dict key ({HASH_MSG})")
expect("set literal", lambda: {S()},
       f"cannot use 'S' as a set element ({HASH_MSG})")

# calling __hash__ directly skips the slot entirely — no validation at all
assert S().__hash__() == "x"

# reverse control: the neighbouring conversions really do use the
# non-<type> template and name the returned type
class I:
    def __index__(self):
        return "x"


class IN:
    def __int__(self):
        return "x"


expect("index", lambda: [0, 1][I()], "__index__ returned non-int (type str)")
expect("int", lambda: int(IN()), "__int__ returned non-int (type str)")

# nothing about a well-behaved __hash__ changes
class Ok:
    def __hash__(self):
        return 42


key = Ok()
assert hash(key) == 42
assert {key: 1}[key] == 1

print("test_hash_result_type_message passed")
