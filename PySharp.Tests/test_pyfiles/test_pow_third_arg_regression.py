"""
Regression: pow(x, y, mod) with a non-integer modulus must raise a catchable
TypeError instead of terminating the process via Debug.Assert.

CPython 3.14 reference:
    pow(2, 3, 1.5)   -> TypeError: pow() 3rd argument not allowed unless all
                        arguments are integers
    pow(1.5, 2, 3)   -> TypeError (same message)
    pow(2, 3, 'a')   -> TypeError: unsupported operand type(s) for ** or pow()
    pow('a', 2, 3)   -> TypeError: unsupported operand type(s) for ** or pow()
    pow(2, 3, 0)     -> ValueError: pow() 3rd argument cannot be 0
    pow(2, -3, 5)    -> 2
    pow(2, 3, None)  -> 8
"""


def check_raises(label, fn, exctype, msg):
    try:
        fn()
    except exctype as e:
        if msg:
            assert msg in str(e), f'{label}: unexpected message: {e}'
        return
    except Exception as e:
        raise AssertionError(f'{label}: expected {exctype.__name__}, got {type(e).__name__}: {e}')
    raise AssertionError(f'{label}: expected {exctype.__name__}, no exception')


# float modulus: TypeError, CPython-identical message (must be catchable)
check_raises('pow(2,3,1.5)', lambda: pow(2, 3, 1.5), TypeError,
             'pow() 3rd argument not allowed unless all arguments are integers')
check_raises('pow(1.5,2,3)', lambda: pow(1.5, 2, 3), TypeError,
             'pow() 3rd argument not allowed unless all arguments are integers')

# non-integer, non-numeric modulus: still a catchable TypeError
# (PySharp raises "3rd argument not allowed" uniformly for non-integer
# moduli; CPython uses "unsupported operand type(s)" here.  Only the
# exception type is asserted so this file also passes on CPython.)
check_raises('pow(2,3,"a")', lambda: pow(2, 3, 'a'), TypeError, '')
check_raises('pow("a",2,3)', lambda: pow('a', 2, 3), TypeError, '')

# zero modulus: ValueError
check_raises('pow(2,3,0)', lambda: pow(2, 3, 0), ValueError,
             'pow() 3rd argument cannot be 0')

# normal cases must stay correct
assert pow(2, 3) == 8
assert pow(2, 3, None) == 8
assert pow(2, 3, 5) == 3
assert pow(2, -3, 5) == 2
assert pow(True, 2, 5) == 1
assert pow(2.0, 3) == 8.0
assert pow(2, 3.0) == 8.0
assert abs(pow(2, -3) - 0.125) < 1e-12
