"""Identifiers written with compatibility characters are NFKC-normalized once in the parser, so they name the same variable as their ASCII expansion.

CPython validates the XID classes and matches keywords on the raw source
text and normalizes afterwards (_PyPegen_new_identifier): the normalized
form becomes the name everywhere (bindings, attributes, error messages)
and is never re-validated. So a fullwidth ｉｆ still assigns a variable
named "if" instead of acting as the if keyword, while a spelling that
collapses to None/True/False raises CPython's identifier-field ValueError.

:kind: test
"""

r"""
Regression for the NFKC half of PEP 3131 support: the XID classes were
already enforced (test_identifier_xid), but names kept their source
spelling, so Ⅷ and VIII were two unrelated variables where CPython has
one, and NameError reported the raw spelling.

1. bindings, loads, deletes and error messages must use the NFKC form:
   Ⅷ binds "VIII", NameError reports 'VIII', globals() holds "VIII".
2. normalization is positional over the whole run: xⅧ -> xVIII,
   ⅧⅧ -> VIIIVIII, and the decomposed A + U+0301 composes to U+00C1.
3. keyword matching stays on the raw text: ｉｆ = 1 binds "if" and
   ｉｆ True: is a plain SyntaxError.
4. the normalized name is not re-validated: ﬁ, ᴬ and Ⅷ bind without
   errors, while a spelling collapsing to None raises
   "identifier field can't represent 'None' constant".
5. XID validation still runs on the raw text before normalization:
   Ⓐ (NFKC 'A') and ﷺ report the original character.
"""


def expect_syntax_error(src, message):
    try:
        compile(src, "<test>", "exec")
    except SyntaxError as e:
        assert message in str(e), (repr(src), str(e))
    else:
        raise AssertionError("expected SyntaxError: " + repr(src))


def expect_value_error(src, message):
    try:
        compile(src, "<test>", "exec")
    except ValueError as e:
        assert message in str(e), (repr(src), str(e))
    else:
        raise AssertionError("expected ValueError: " + repr(src))


# 1. one name under both spellings, messages included
Ⅷ = 1
assert VIII == 1
VIII = 2
assert Ⅷ == 2
assert "Ⅷ" not in globals()
assert globals()["VIII"] == 2

try:
    print(Ⅸ)
except NameError as e:
    assert "name 'IX' is not defined" in str(e), str(e)
else:
    raise AssertionError("expected NameError")

del Ⅷ
assert "VIII" not in globals()

Ⅸ = 9
assert IX == 9
del Ⅸ
assert "IX" not in globals()

# 2. positional normalization and composition
xⅧ = 1
assert xVIII == 1
del xⅧ

ⅧⅧ = 2
assert VIIIVIII == 2
del ⅧⅧ
assert "VIIIVIII" not in globals()

ns = {}
exec("A\u0301 = 3", ns)
assert ns["\u00c1"] == 3
assert len(ns) == 2, sorted(ns)  # __builtins__ plus the composed name

# 3. keyword matching runs on the raw spelling
kw_ns = {}
exec("ｉｆ = 1", kw_ns)
assert kw_ns["if"] == 1
assert "ｉｆ" not in kw_ns

try:
    exec("ｉｆ True:\n    pass")
except SyntaxError:
    pass
else:
    raise AssertionError("expected SyntaxError for fullwidth ｉｆ statement")

# 4. the normalized name is never re-validated
ﬁ = 1
assert fi == 1
del ﬁ

ａ = 2
assert a == 2
del ａ

ᴬ = 3
assert A == 3
del ᴬ

expect_value_error("Ｎｏｎｅ = 1\n", "identifier field can't represent 'None' constant")
expect_value_error("Ｔｒｕｅ = 1\n", "identifier field can't represent 'True' constant")
expect_value_error("Ｆａｌｓｅ = 1\n", "identifier field can't represent 'False' constant")

# 5. XID validation keeps running on the raw spelling
expect_syntax_error("x = Ⓐ\n", "invalid character 'Ⓐ' (U+24B6)")
expect_syntax_error("Ⓐx = 1\n", "invalid character 'Ⓐ' (U+24B6)")
expect_syntax_error("x = ﷺ\n", "invalid character 'ﷺ' (U+FDFA)")

# every identifier position normalizes
def ﬁ():
    return 7


assert fi() == 7

def collect(**kw):
    return sorted(kw)


assert collect(Ⅷ=1) == ["VIII"]
assert collect(ﬁ=1, ａ=2) == ["a", "fi"]


def fn(Ⅷ):
    return Ⅷ


assert fn(5) == 5


class Ⅷ:
    pass


assert Ⅷ.__name__ == "VIII"

class Box:
    pass


box = Box()
box.Ⅷ = 5
assert box.VIII == 5

Ⅷ: int = 3
assert VIII == 3
del Ⅷ

if (Ⅷ := 8) == 8:
    pass
assert VIII == 8
del Ⅷ

Ⅷ = 41
match 41:
    case Ⅷ:
        pass
assert VIII == 41
del Ⅷ

ﬁ = 1
ａ = 2
assert f"{ﬁ}{ａ}" == "12"
del ﬁ, ａ


def set_global():
    global Ⅸ
    Ⅸ = 99


set_global()
assert IX == 99
del Ⅸ

print("test_identifier_nfkc passed")
