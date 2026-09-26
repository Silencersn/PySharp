"""In-place pow protocol semantics.

CPython's slot_nb_inplace_power drops the modulus entirely and always
calls __ipow__(self, other) with two arguments; **= falls back to the
plain pow dispatch (__pow__/__rpow__) when __ipow__ is not defined.
Argument-mismatch failures only assert the exception type here because
the message text is tracked separately.

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


class P:
    def __init__(self, v):
        self.v = v
    def __ipow__(self, other):
        self.v **= other
        return self

class P3:
    def __init__(self, v):
        self.v = v
    def __ipow__(self, other, mod):
        self.v **= other
        return self

class OnlyPow:
    def __init__(self, v):
        self.v = v
    def __pow__(self, other):
        return OnlyPow(self.v ** other)


def ipow_value():
    p = P(3)
    p **= 2
    return p.v

def ipow_identity():
    p = P(3)
    q = p
    p **= 2
    return p is q

def ipow_three_args():
    p = P3(3)
    p **= 2
    return p.v

def ipow_fallback():
    p = OnlyPow(3)
    p **= 2
    return p.v

def pow_ignores_ipow():
    try:
        return pow(P(3), 2)
    except TypeError:
        return "TypeError"

def int_ipow():
    x = 5
    x **= 2
    return x

def float_ipow():
    x = 2.0
    x **= 3
    return x


expect_value("p **= 2 calls __ipow__", ipow_value, 9)
expect_value("__ipow__ keeps identity", ipow_identity, True)
expect_type_error("three-arg __ipow__ with **=", ipow_three_args)
expect_value("**= falls back to __pow__", ipow_fallback, 9)
expect_value("pow() ignores __ipow__", pow_ignores_ipow, "TypeError")
expect_value("int **=", int_ipow, 25)
expect_value("float **=", float_ipow, 8.0)

print("ipow protocol passed")
