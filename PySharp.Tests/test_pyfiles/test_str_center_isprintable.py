"""str.center places the odd remainder column LEFT when margin and width are both odd, and str.isprintable treats all Unicode Other/Separator characters as non-printable except ASCII space.

:kind: test
"""

# str.center: the odd remainder column goes LEFT when both the margin
# and the width are odd (CPython unicode_center_impl:
# left = marg // 2 + (marg & width & 1)).
assert "ab".center(5, '*') == '**ab*'
assert "hi".center(5) == '  hi '
assert "abcd".center(9) == '   abcd  '
assert "hi".center(7, '>') == '>>>hi>>'
assert "xx".center(7) == '   xx  '
# Even margins and marg&width&1 == 0 keep the split at marg // 2.
assert "ab".center(6, '*') == '**ab**'
assert "abc".center(4) == 'abc '
assert "a".center(4) == ' a  '
assert "x".center(2) == 'x '
assert "xxx".center(8) == '  xxx   '

# str.isprintable: characters in the Unicode "Other" or "Separator"
# categories are non-printable; the ASCII space is the only exception
# (Py_UNICODE_ISPRINTABLE).
assert "a\tb".isprintable() is False
assert "\n".isprintable() is False
assert "\r\v\f".isprintable() is False
assert "\t".isprintable() is False
assert " ".isprintable() is True
assert "a b".isprintable() is True
assert "a\u00a0b".isprintable() is False  # NBSP, Zs
assert "a\u3000b".isprintable() is False  # ideographic space, Zs
assert "a\x1bb".isprintable() is False  # ESC, Cc
assert "a\x7fb".isprintable() is False  # DEL, Cc
assert "a\u200bb".isprintable() is False  # ZWSP, Cf
assert "a\u2028b".isprintable() is False  # LINE SEPARATOR, Zl
assert "a\U000E0000b".isprintable() is False  # unassigned, Cn
assert "a\uf8ffb".isprintable() is False  # private use, Co
assert "".isprintable() is True
assert "abc".isprintable() is True
print("ok")
