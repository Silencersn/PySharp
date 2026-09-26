"""
a bare ':' inside an f-string replacement field ends the
expression and starts the format spec, even when lexed as the fused ':='
token. f"{-5:=8}" was rejected with a SyntaxError; it must equal
format(-5, "=8"). A walrus needs parentheses (PEP 572): f"{(x := 8)}".

CPython 3.14 reference:
    f"{-5:=8}" == "-      5"   (spec "=8")
    f"{7:=8.2f}" == "    7.00", f"{-5::>8}" == "::::::-5"
    f"{7=}" == "7=7" (debug '=' unchanged), f"{x=:8}" is debug + spec
    t"{7:=8}" interpolations[0].format_spec == "=8"

:kind: test
"""

assert f"{-5:=8}" == format(-5, "=8") == "-      5"
assert f"{5:=8}" == "       5"
assert f"{-5:=}" == "-5"
assert f"{-5::>8}" == "::::::-5"
assert f"{7:=8.2f}" == "    7.00"
assert f"{7:=,}" == "7"
assert f"{-5:=+8}" == "-      5"
assert f"{-5:->+8}" == "-------5"
x = 7
assert f"{x:=8}" == "       7"
assert f"{x :=8}" == "       7"
assert f"{x:= 8}" == "       7"
assert f"{x:=}" == "7"
assert f"{x: =}" == "7"
assert f"{x:= }" == " 7"

# debug '=' forms unchanged
assert f"{7=}" == "7=7"
assert f"{7 =}" == "7 =7"
assert f"{x=:8}" == "x=       7"

# nested replacement field in a ':=' spec
assert f"{-5:=>{8-1}}" == "=====-5"

# walrus still requires parentheses and still works there
assert f"{(x := 8)}" == "8" and x == 8

# slices, subscripts and lambda colons inside expressions are unaffected
d = {'a': 1}
assert f"{d['a']}" == "1"
assert f"{[1, 2, 3][0:2]}" == "[1, 2]"
assert f"{(lambda y: y)(5)}" == "5"

# a string value still evaluates the spec ('=' alignment rejected)
try:
    f"{'ab':=8}"
    assert False, "ValueError expected"
except ValueError:
    pass

# t-strings share the rule
t = t"{7:=8}"
i = t.interpolations[0]
assert str(i.format_spec) == "=8"
assert str(i.expression) == "7"
assert i.value == 7

print("test_fstring_colonequal_spec passed")
