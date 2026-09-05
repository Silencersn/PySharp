"""
Regression: an f-string debug specifier (`{expr=}`) without an explicit
conversion and without a format spec must default to repr(), like
CPython. PySharp used to apply no conversion at all (str() semantics),
so string values lost their quotes.

CPython 3.14 reference (Parser/action_helpers.c
_get_interpolation_conversion):
    debug && !conversion && !format_spec  ->  conversion = 'r'

    s = "hi"
    f"{s=}"      -> "s='hi'"
    f"{s = }"    -> "s = 'hi'"
    f"{f'x'=}"   -> "f'x'='x'"

Forms with an explicit conversion, or values whose str() equals repr(),
already match CPython (kept below as guards).
"""

s = "hi"

# no conversion, no format spec: must use repr
assert f"{s=}" == "s='hi'"
assert f"{s = }" == "s = 'hi'"

# custom object whose str() differs from repr()
class P:
    def __repr__(self):
        return "P()"
    def __str__(self):
        return "plain"

p = P()
assert f"{p=}" == "p=P()"

# nested f-string as the debug expression
assert f"{f'x'=}" == "f'x'='x'"

# --- guards: explicit conversion / str==repr values ---
assert f"{s=!r}" == "s='hi'"
assert f"{s=!s}" == "s=hi"

b = b'x'
assert f"{b=}" == "b=b'x'"

n = 5
assert f"{n=}" == "n=5"

print("test_fstring_debug_repr_regression passed")
