"""% formatting names an unsupported format character with a ? for non-printables and the at-index position in the format string.

:kind: test
"""

# Regression: CPython reports "unsupported format character 'z' (0x7a)
# at index 5" — the character prints itself only when printable ASCII
# (control and non-ascii characters show '?', the hex code point always
# shows the real value), and the index points into the format string,
# shifted by literal text, %% escapes, mapping keys, flags and precision.


def show(s, arg=1):
    try:
        print(repr(s), "->", repr(s % arg))
    except ValueError as e:
        print(repr(s), "!", repr(str(e)))


show("%z")
show("abc %z def")
show("%%%%%z")
show("%d and %z", (1, 2))
show("%(a)z", {'a': 1})
show("%.2z")
show("%é")
show("%\t")
show("%\x01")
show("% ")
show("100%%z", ())
show("%10.2z")
show("%-+05z")
show("x%(k)#.3z", {'k': 1})
