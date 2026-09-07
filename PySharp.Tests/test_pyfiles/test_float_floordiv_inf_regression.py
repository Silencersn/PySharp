# float floor-division and divmod regression: CPython derives both results
# from the _float_div_mod fmod algorithm (floatobject.c), not from
# Floor(a/b) - infinite operands yield nan (inf // 2 == nan, divmod(inf, 2)
# == (nan, nan)) and a zero quotient takes the sign of the true quotient
# (-2 // inf == -1.0, not -0.0).

inf = float('inf')
nan = float('nan')

assert inf // 2 != inf  # must be nan
assert str(inf // 2) == "nan"
assert str(-inf // 2) == "nan"
assert str(inf // -2) == "nan"
assert str(inf // 2.5) == "nan"
assert str(inf // inf) == "nan"
assert str(inf // nan) == "nan"
assert str(nan // 2) == "nan"

# finite numerators over an infinite divisor: quotient snaps to the true
# quotient's sign, not Floor(-0.0)
assert 2.0 // inf == 0.0
assert -2.0 // inf == -1.0
assert -2 // inf == -1.0
assert 1 // inf == 0.0
assert 2.5 // inf == 0.0

# ordinary floor division unchanged
assert 7.0 // 2 == 3.0
assert -7.0 // 2 == -4.0
assert 7.0 // -2 == -4.0

# divmod agrees with the same algorithm
q, m = divmod(inf, 2)
assert str(q) == "nan" and str(m) == "nan", (q, m)
q, m = divmod(-inf, 2)
assert str(q) == "nan" and str(m) == "nan", (q, m)
assert divmod(2, inf) == (0.0, 2.0)
assert divmod(-2, inf) == (-1.0, inf)
q, m = divmod(inf, inf)
assert str(q) == "nan" and str(m) == "nan", (q, m)
q, m = divmod(nan, 2)
assert str(q) == "nan" and str(m) == "nan", (q, m)

print("ok")
