"""Binary-operator TypeErrors carry CPython's exact message templates: the generic unsupported-operand form, the '** or pow()' display, augmented in-place names, the sequence concatenate family after reflected decline, and the container message when iter() raises TypeError inside `in`.

:kind: test
"""

# Regression: binary-operator TypeErrors follow CPython's message
# templates — binop_type_error's "unsupported operand type(s) for OP:"
# with "** or pow()" for pow and the augmented form ("+=", "**=") for
# in-place, the per-type concatenate family raised by the left operand's
# sq_concat after reflection declines, and _PySequence_IterSearch's
# container message when iter() fails with TypeError inside `in`.

def raises_type_error(src, expected):
    try:
        exec(src, globals())
    except TypeError as e:
        assert str(e) == expected, (src, str(e), expected)
    else:
        raise AssertionError("expected TypeError for " + src)

# the generic template across the arithmetic/bitwise family
for src, op in [
    ("5 + None", "+"),
    ("5 - None", "-"),
    ("5 / None", "/"),
    ("5 // None", "//"),
    ("5 % None", "%"),
    ("5 << None", "<<"),
    ("5 >> None", ">>"),
    ("5 & None", "&"),
    ("5 | None", "|"),
    ("5 ^ None", "^"),
]:
    raises_type_error(src, f"unsupported operand type(s) for {op}: 'int' and 'NoneType'")

raises_type_error("None * 5", "unsupported operand type(s) for *: 'NoneType' and 'int'")
raises_type_error("divmod(5, 'a')", "unsupported operand type(s) for divmod(): 'int' and 'str'")

# pow shares one display across ** and pow()
raises_type_error("2 ** 'a'", "unsupported operand type(s) for ** or pow(): 'int' and 'str'")
raises_type_error("pow(2, 'a')", "unsupported operand type(s) for ** or pow(): 'int' and 'str'")
raises_type_error("5 @ 5", "unsupported operand type(s) for @: 'int' and 'int'")

# in-place errors name the augmented form; pow keeps **= only
for src, op in [
    ("x = 5\nx += None", "+="),
    ("x = 5\nx -= None", "-="),
    ("x = 5\nx *= None", "*="),
    ("x = 5\nx /= None", "/="),
    ("x = 5\nx //= None", "//="),
    ("x = 5\nx %= None", "%="),
    ("x = 1\nx <<= None", "<<="),
    ("x = 1\nx >>= None", ">>="),
    ("x = 1\nx &= None", "&="),
    ("x = 1\nx |= None", "|="),
    ("x = 1\nx ^= None", "^="),
    ("x = 5\nx @= None", "@="),
]:
    raises_type_error(src, f"unsupported operand type(s) for {op}: 'int' and 'NoneType'")
raises_type_error("x = 2\nx **= 'a'", "unsupported operand type(s) for **=: 'int' and 'str'")

# the concatenate family: the left operand's sq_concat raises after
# the reflected handler declines
raises_type_error("[1] + (2,)", 'can only concatenate list (not "tuple") to list')
raises_type_error("[1] + 5", 'can only concatenate list (not "int") to list')
raises_type_error("() + 5", 'can only concatenate tuple (not "int") to tuple')
raises_type_error("'a' + None", 'can only concatenate str (not "NoneType") to str')
raises_type_error("b'a' + 'b'", "can't concat str to bytes")
raises_type_error("bytearray(b'a') + 'b'", "can't concat str to bytearray")

# sets have no sq_concat: the generic template applies
raises_type_error("frozenset() + frozenset()", "unsupported operand type(s) for +: 'frozenset' and 'frozenset'")
raises_type_error("set() + set()", "unsupported operand type(s) for +: 'set' and 'set'")

# the reflected __radd__ still runs before the concat TypeError
class Weird:
    def __radd__(self, other):
        return ("radd", other)

assert [1] + Weird() == ("radd", [1])
assert () + Weird() == ("radd", ())
assert 'a' + Weird() == ("radd", 'a')
assert b'a' + Weird() == ("radd", b'a')
assert bytearray(b'a') + Weird() == ("radd", bytearray(b'a'))
raises_type_error("Weird() + [1]", "unsupported operand type(s) for +: 'Weird' and 'list'")

# `in` over an object without __contains__ replaces a TypeError from
# iter() with the container message; other errors propagate
class Plain:
    pass
class BoomIter:
    def __iter__(self):
        raise TypeError("boom")
class ValIter:
    def __iter__(self):
        raise ValueError("nope")

raises_type_error("'a' in 5", "argument of type 'int' is not a container or iterable")
raises_type_error("'a' in Plain()", "argument of type 'Plain' is not a container or iterable")
raises_type_error("'a' in BoomIter()", "argument of type 'BoomIter' is not a container or iterable")
raises_type_error("'a' in 5.5", "argument of type 'float' is not a container or iterable")
try:
    'a' in ValIter()
    raise AssertionError("expected ValueError")
except ValueError as e:
    assert str(e) == "nope"

# working containers keep scanning
assert 'a' in ['a'] and 'a' not in [1] and 1 in {1: 2}

print("test_binop_error_messages passed")
