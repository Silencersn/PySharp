r"""
Regression: identifier character classes must follow CPython's
XID_Start / XID_Continue tables (UAX #31 plus the NFKC closure), not the
.NET \w class the PySharp lexer matched:

1. Nd characters (١, U+0661) were accepted as the FIRST char of a name;
   CPython rejects the first one with
   "invalid character '١' (U+0661)".
2. Nl characters (Ⅷ, U+2167) were rejected as a bare "invalid syntax";
   Nl belongs to XID_Start, CPython accepts the name.
3. non-identifier characters collapsed to "invalid syntax" instead of
   CPython's "invalid character '<c>' (U+XXXX)" form, and unprintable
   code points lacked the dedicated
   "invalid non-printable character U+XXXX" form.

Guards: Nd, Other_ID_Continue (U+00B7) and Join_Control (U+200D) stay
legal after the first char, and a non-BMP letter (U+20000) is accepted.

The NFKC normalisation of names (Ⅷ becomes 'VIII' in CPython) is a
separate gap and is deliberately not asserted here.
"""


def expect_invalid_character(src, message):
    try:
        compile(src, "<test>", "exec")
    except SyntaxError as e:
        assert message in str(e), (src, str(e))
    else:
        raise AssertionError("expected SyntaxError: " + repr(src) + " -> " + message)


def expect_non_printable_character(src, message):
    try:
        compile(src, "<test>", "exec")
    except SyntaxError as e:
        assert message in str(e), (src, str(e))
    else:
        raise AssertionError("expected SyntaxError: " + repr(src) + " -> " + message)


# red case 1: Nd may not start an identifier
expect_invalid_character("x = ١\n", "invalid character '١' (U+0661)")
expect_invalid_character("١abc = 1\n", "invalid character '١' (U+0661)")
expect_invalid_character("x = ١٢\n", "invalid character '١' (U+0661)")

# red case 2: the CPython invalid character form must be used for the
# other rejected classes too (No, Po, Lm excluded from XID, non-BMP)
expect_invalid_character("x = ²\n", "invalid character '²' (U+00B2)")
expect_invalid_character("x = ¹\n", "invalid character '¹' (U+00B9)")
expect_invalid_character("x = ·\n", "invalid character '·' (U+00B7)")
expect_invalid_character("x = ・\n", "invalid character '・' (U+30FB)")
expect_invalid_character("x = ゛\n", "invalid character '゛' (U+309B)")
expect_invalid_character("x = 😀\n", "invalid character '😀' (U+1F600)")

# red case 2b: unprintable code points have their own message form, for
# non-ASCII code points and for ASCII control characters alike
expect_non_printable_character("x = \u200d\n", "invalid non-printable character U+200D")
expect_non_printable_character("x = \ufeff\n", "invalid non-printable character U+FEFF")
expect_non_printable_character("x = \xa0\n", "invalid non-printable character U+00A0")
expect_non_printable_character("x = \x01\n", "invalid non-printable character U+0001")
expect_non_printable_character("x = \x7f\n", "invalid non-printable character U+007F")

# red case 3: Nl is XID_Start, these names must be accepted
Ⅷ = 5
assert Ⅷ == 5
Ⅸ = Ⅷ + 1
assert Ⅸ == 6
〇 = 7
assert 〇 == 7

# guards: continuation characters stay legal
x١ = 1
assert x١ == 1
a·b = 2
assert a·b == 2
_١ = 3
assert _١ == 3
𠀀 = 4
assert 𠀀 == 4
π = 3
assert π == 3

print("test_identifier_xid_regression passed")
