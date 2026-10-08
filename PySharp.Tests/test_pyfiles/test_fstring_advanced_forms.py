"""
Advanced f-string forms: triple-quoted literals inside replacement fields,
nested replacement fields carrying their own format spec, and backslash
handling before braces in raw f-strings.

:kind: test
"""


def check(src):
    try:
        compile(src, "<s>", "exec")
        return "OK"
    except SyntaxError as e:
        return str(e.msg)


x = "v"

# a plain triple-quoted literal inside a replacement field
assert f"{'''{x}'''}" == "{x}"
assert f'{'''a'''.upper()}' == "A"
assert f"""{ '''b''' + 'c' }""" == "bc"

# a nested f-string written as a triple-quoted literal
assert f"{f'''inner {x}'''}" == "inner v"

# multi-line expressions with comments and wrapped parentheses
r = f"{
    x + 'w'  # trailing comment
}"
assert r == "vw"
r = f"""{
    1 + (
        2
    )
}"""
assert r == "3"

# a nested replacement field carrying its own format spec
w = 8
p = 2
v = 3.14159
assert f"{v:{w}.{p}}" == "     3.1"
assert f"[{v:{w:0}.{p:1}f}]" == "[    3.14]"
assert f"{v:{w:d}}" == " 3.14159"

# a dict literal's colon becomes the nested field's spec separator, just
# like CPython's tokenizer (the '>' lands in the spec, not in a dict)
try:
    f"{v:{'x':>{w}}}"
    assert False
except ValueError as e:
    assert str(e) == "Invalid format specifier '       x' for object of type 'float'"

# a backslash never consumes a following brace: "{{" still escapes and a
# raw "\{" opens a replacement field
q = "X"
assert rf"\{q}" == "\\X"
assert rf"\{{q}}" == "\\{q}"
assert rf"\{{{q}}}" == "\\{X}"
assert rf"\d+ {1 + 1}" == "\\d+ 2"
assert rf"\}}" == "\\}"

# unterminated fields and rejected closers keep CPython's wording
assert check('f"{"') == "f-string: expecting '}'"
assert check('f"""{"""') == "f-string: expecting '}'"
assert check('f"\\}"') == "f-string: single '}' is not allowed"
assert check('f"}"') == "f-string: single '}' is not allowed"
assert check('f"{x"""') == "unterminated triple-quoted string literal (detected at line 1)"

print("ok")
