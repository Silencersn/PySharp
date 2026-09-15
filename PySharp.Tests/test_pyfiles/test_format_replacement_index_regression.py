# Regression: str.format's out-of-range positional field raises
# CPython's "Replacement index N out of range for positional args
# tuple" (carrying the field index) for both automatic and manual
# numbering, instead of the generic "tuple index out of range".
MSG = "Replacement index {} out of range for positional args tuple"

try:
    "{} {}".format(1)
    raise AssertionError("expected IndexError")
except IndexError as e:
    assert str(e) == MSG.format(1), str(e)

try:
    "{2}".format(1, 2)
    raise AssertionError("expected IndexError")
except IndexError as e:
    assert str(e) == MSG.format(2), str(e)

try:
    "{}".format()
    raise AssertionError("expected IndexError")
except IndexError as e:
    assert str(e) == MSG.format(0), str(e)

try:
    "{0} {0}".format()
    raise AssertionError("expected IndexError")
except IndexError as e:
    assert str(e) == MSG.format(0), str(e)

try:
    "{1} {3}".format(1, 2)
    raise AssertionError("expected IndexError")
except IndexError as e:
    assert str(e) == MSG.format(3), str(e)

# neighboring faces stay intact
assert "{0} {x}".format(1, x=2) == "1 2"
try:
    "{k}".format()
    raise AssertionError("expected KeyError")
except KeyError as e:
    assert str(e) == "'k'"
try:
    "{0[1]}".format(1)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert str(e) == "'int' object is not subscriptable"

print("test_format_replacement_index_regression passed")
