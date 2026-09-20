"""
Regression test: chr() returns a lone surrogate for a code point in
U+D800-U+DFFF, and ord()/repr()/ascii() read it back unchanged.

CPython 3.14 reference:
    chr(0xD800)       -> one-character string holding the surrogate
    ord(chr(0xD800))  -> 0xD800
    repr(chr(0xD800)) -> the escaped u-form, never U+FFFD
    chr(0x110000)     -> ValueError

This docstring spells the escapes out in prose: a raw surrogate in a
module docstring makes CPython itself fail to compile the module.
"""

# Below / above the surrogate range: normal behavior
assert chr(0xD7FF) == '\ud7ff'
assert chr(0xE000) == '\ue000'
assert chr(0x10FFFF) == '\U0010ffff'
assert len(chr(0x10FFFF)) == 1

# Surrogate range: a lone surrogate, not U+FFFD
for cp in (0xD800, 0xDBFF, 0xDC00, 0xDFFF):
    s = chr(cp)
    assert len(s) == 1, f"len(chr({cp:#x})) == {len(s)}"
    assert ord(s) == cp, f"ord(chr({cp:#x})) == {ord(s):#x}"
    assert s == chr(cp)
    assert s != '\ufffd', f"chr({cp:#x}) must not be the replacement character"
    assert repr(s) == "'\\u%04x'" % cp, f"repr(chr({cp:#x})) == {repr(s)}"
    assert ascii(s) == "'\\u%04x'" % cp, f"ascii(chr({cp:#x})) == {ascii(s)}"

# Out of range stays a ValueError
try:
    chr(0x110000)
    assert False, "chr(0x110000) should raise ValueError"
except ValueError:
    pass

print("test_chr_surrogate_regression passed")
