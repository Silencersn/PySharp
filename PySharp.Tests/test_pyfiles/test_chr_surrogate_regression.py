"""
Regression test for issue #42: chr() must not silently return U+FFFD for a
surrogate code point (U+D800-U+DFFF).

CPython 3.14 reference:
    chr(0xD800)   -> '\ud800'   (lone surrogate, len 1)
    chr(0xDFFF)   -> '\udfff'
    chr(0xD7FF)   -> '\ud7ff'   (below the surrogate range -> normal)
    chr(0xE000)   -> '\ue000'   (above the surrogate range -> normal)
    chr(0x10FFFF) -> '\U0010ffff'

PySharp decision (mitigation for #42):
    PySharp cannot yet keep lone surrogates intact through the whole str
    pipeline (ord/repr/ascii iterate with EnumerateRunes, which replaces
    unpaired surrogates with U+FFFD). Instead of silently returning the wrong
    U+FFFD, chr() explicitly raises the internal PySharpException for the
    surrogate range. Non-surrogate code points behave normally.

    Note: PySharpException is not registered in builtins, so the test cannot
    reference it by name; it checks the exception type name instead.
"""

# Below / above the surrogate range: normal behavior
assert chr(0xD7FF) == '\ud7ff'
assert chr(0xE000) == '\ue000'
assert chr(0x10FFFF) == '\U0010ffff'
assert len(chr(0x10FFFF)) == 1

# Surrogate range (U+D800-U+DFFF): explicitly rejected
for cp in (0xD800, 0xDBFF, 0xDC00, 0xDFFF):
    try:
        chr(cp)
        assert False, f"chr({cp:#x}) should raise PySharpException for a surrogate code point"
    except BaseException as e:
        assert type(e).__name__ == 'PySharpException', \
            f"chr({cp:#x}) raised unexpected {type(e).__name__}: {e}"

print("test_chr_surrogate_regression passed")
