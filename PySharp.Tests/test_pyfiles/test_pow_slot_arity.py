"""Pow slot arity validation.

CPython's slot_nb_power passes two arguments to Python-level __pow__ and
__rpow__ for the binary form (x ** y, pow(x, y), pow(x, y, None)) and
three arguments only for the ternary pow(x, y, mod) form, regardless of
the method signature: a two-argument method is never fed a None modulus,
and a three-argument-only method fails on the binary form with a
missing-argument TypeError. Argument-mismatch failures only assert the
exception type here because the message text is tracked separately.

:kind: test
"""

def expect_value(label, fn, expected):
    result = fn()
    assert result == expected, f"{label}: expected {expected!r}, got {result!r}"

def expect_type_error(label, fn):
    try:
        fn()
    except TypeError:
        pass
    else:
        raise AssertionError(f"{label}: expected TypeError")


class RS:
    def __rpow__(self, other):
        return "rpow2"

class RS3:
    def __rpow__(self, other, mod):
        return ("rpow3", mod)

class D(RS):
    pass

class F:
    def __pow__(self, other):
        return 42

class F3:
    def __pow__(self, other, mod=None):
        return (42, mod)


rs = RS()
rs3 = RS3()
f = F()
f3 = F3()

# reflected pow: binary forms pass no modulus
expect_value("5 ** RS()", lambda: 5 ** rs, "rpow2")
expect_value("pow(5, RS())", lambda: pow(5, rs), "rpow2")
expect_value("pow(5, RS(), None)", lambda: pow(5, rs, None), "rpow2")
expect_value("RS().__rpow__(5) direct call", lambda: rs.__rpow__(5), "rpow2")
expect_value("inherited __rpow__ via 5 ** D()", lambda: 5 ** D(), "rpow2")

# reflected pow: ternary form forwards the modulus
expect_value("pow(5, RS3(), 3)", lambda: pow(5, rs3, 3), ("rpow3", 3))
expect_type_error("5 ** RS3() needs mod", lambda: 5 ** rs3)
expect_type_error("pow(5, RS(), 3) feeds three args", lambda: pow(5, rs, 3))

# forward pow: same binary/ternary split
expect_value("F() ** 2", lambda: f ** 2, 42)
expect_value("pow(F(), 2)", lambda: pow(f, 2), 42)
expect_type_error("pow(F(), 2, 3) feeds three args", lambda: pow(f, 2, 3))
expect_value("F3() ** 2 keeps mod default", lambda: f3 ** 2, (42, None))
expect_value("pow(F3(), 2, 3)", lambda: pow(f3, 2, 3), (42, 3))

print("pow slot arity passed")
