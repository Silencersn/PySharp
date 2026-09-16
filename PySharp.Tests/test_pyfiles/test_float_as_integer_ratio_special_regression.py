# Regression test: float.as_integer_ratio() raises OverflowError for
# infinities and ValueError for NaN, with CPython's dedicated messages
# (the two exception types are deliberately different).
for bad, expected in [
    (float('inf'), 'cannot convert Infinity to integer ratio'),
    (float('-inf'), 'cannot convert Infinity to integer ratio'),
]:
    try:
        bad.as_integer_ratio()
    except OverflowError as e:
        assert str(e) == expected, (bad, e)
    except ValueError as e:
        raise AssertionError(f'inf raised ValueError, not OverflowError: {e}')
    else:
        raise AssertionError(bad)

try:
    float('nan').as_integer_ratio()
except OverflowError as e:
    raise AssertionError(f'nan raised OverflowError, not ValueError: {e}')
except ValueError as e:
    assert str(e) == 'cannot convert NaN to integer ratio', e
else:
    raise AssertionError('nan')

# finite control faces stay intact
assert (1.5).as_integer_ratio() == (3, 2)
assert (-0.0).as_integer_ratio() == (0, 1)
assert (10.0).as_integer_ratio() == (10, 1)
assert (-0.25).as_integer_ratio() == (-1, 4)
assert (2 ** -1074).as_integer_ratio() == (1, 2 ** 1074)

print("test_float_as_integer_ratio_special_regression passed")
