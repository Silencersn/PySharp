# Regression: float repr renders the shortest round-trip digit string
# per CPython dtoa mode 0 — exact BigInteger math instead of .NET's
# shortest mode, which emits one digit too few on ties like 2**-25
# (its 16-digit string parses back to the lower neighbor). Presentation
# (format_float_short): scientific iff the decimal point sits at
# position <= -4 or > 16, exponent >= 2 digits, integral ".0".
# All pinned strings are CPython 3.14 ground truth.

# --- the issue's face ---
assert repr(2**-25) == "2.9802322387695312e-08"
assert repr(-2**-25) == "-2.9802322387695312e-08"
# the 16-digit string parses to the lower neighbor, so 17 digits are
# the shortest round-tripping form
assert float("2.980232238769531e-08") != 2**-25
assert float(repr(2**-25)) == 2**-25

# --- power-of-two family (quarter-ulp cell floor, exact-tie cuts) ---
assert repr(2**-1074) == "5e-324"
assert repr(2.0**-1073) == "1e-323"
assert repr(2**-100) == "7.888609052210118e-31"
assert repr(2**-35) == "2.9103830456733704e-11"
assert repr(2**-15) == "3.0517578125e-05"
assert repr(2.0**15) == "32768.0"
assert repr(2.0**53) == "9007199254740992.0"
assert repr(2.0**53 + 1.0) == "9007199254740992.0"
assert repr(2.0**1023) == "8.98846567431158e+307"

# --- presentation boundaries ---
assert repr(1e15) == "1000000000000000.0"
assert repr(1e16) == "1e+16"
assert repr(1e17) == "1e+17"
assert repr(0.0001) == "0.0001"
assert repr(0.001) == "0.001"
assert repr(1e-5) == "1e-05"
assert repr(1e308) == "1e+308"
assert repr(1.7976931348623157e308) == "1.7976931348623157e+308"
assert repr(0.0) == "0.0" and repr(-0.0) == "-0.0"

# --- ordinary shortest digits ---
assert repr(0.1) == "0.1"
assert repr(0.3) == "0.3"
assert repr(1 / 3) == "0.3333333333333333"
assert repr(199.5) == "199.5"
assert repr(300.0) == "300.0"
assert repr(2**-25 * 3) == "8.940696716308594e-08"
assert str(0.1) == repr(0.1)

# --- previously mismatching dtoa faces (multi-candidate cells) ---
assert repr(7.120236347223045e-307) == "7.120236347223045e-307"
assert repr(7.291122019556398e-304) == "7.291122019556398e-304"
assert repr(5.641232424577593e-278) == "5.641232424577593e-278"
assert repr(3.573152451604843e-283) == "3.573152451604843e-283"

# --- round-trip property over a deterministic sample ---
seed = 0x243F6A8885A308D3
for _ in range(500):
    seed = (seed * 6364136223846793005 + 1442695040888963407) % (1 << 64)
    i = (seed >> 8) % (1 << 53)
    k = (seed >> 33) % 400 - 200
    d = i * 2.0**k
    if d != d or d in (float("inf"), float("-inf")):
        continue
    assert float(repr(d)) == d, (repr(d),)

# --- %r and format() share the shortest rendering ---
assert "%r" % 2**-25 == "2.9802322387695312e-08"
assert format(2**-25) == "2.9802322387695312e-08"
assert repr(complex(2**-25, 1.0)) == "(2.9802322387695312e-08+1j)"

print("test_float_repr_shortest_regression passed")
