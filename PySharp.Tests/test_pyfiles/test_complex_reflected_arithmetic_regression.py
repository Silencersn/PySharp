"""
Regression: int/float/bool on the LEFT of +, -, *, / and ** with a complex
right operand must dispatch the complex reflected slots (CPython's
order-agnostic complex slots) instead of raising TypeError — `1 + 1j` and
friends used to fail while complex-on-left worked. Forward complex **
(complex_pow with c_powi repeated squaring for small exact-integer
exponents and the c_pow log/exp form otherwise) is covered alongside, since
the reflected ** depends on it. Floor-division/modulo/divmod and ordered
comparisons keep their TypeErrors.

CPython 3.14.6 reference:
    1 + 1j        -> (1+1j)      1 / 1j        -> -1j
    2 * 1j        -> 2j          True + 1j     -> (1+1j)
    5 - 1j        -> (5-1j)      2 ** 1j       -> (0.7692389013639721+0.6389612763136348j)
    1j ** 2       -> (-1+0j)     1j ** 1j      -> (0.20787957635076193+0j)
    1j ** -2      -> (-1-0j)     0 ** 1j       -> ZeroDivisionError
    pow(2, 1j, 5) -> ValueError: complex modulo
    5 % 1j        -> TypeError: unsupported operand type(s) for %: 'int' and 'complex'
"""

# reflected arithmetic: real/bool on the left
assert str(1 + 1j) == '(1+1j)'
assert str(1j + 1) == '(1+1j)'
assert str(2 * 1j) == '2j'
assert str(1j * 2) == '2j'
assert str(5 - 1j) == '(5-1j)'
assert str(1 - 1j) == '(1-1j)'
assert str(1.5 + 1j) == '(1.5+1j)'
assert str(1.5 - 1j) == '(1.5-1j)'
assert str(2.0 * 1j) == '2j'
assert str(1.5 / 1j) == '-1.5j'
assert str(1 / 1j) == '-1j'
assert str(True + 1j) == '(1+1j)'
assert str(True * 1j) == '1j'
assert repr(1.5 ** 1j) == '(0.9189190364394997+0.3944461997143608j)'
assert (type(1 + 1j).__name__, type(2 ** 1j).__name__) == ('complex', 'complex')

# reflected and forward power share the small-integer squaring path
assert repr(2 ** 1j) == '(0.7692389013639721+0.6389612763136348j)'
assert repr(2.0 ** 1j) == '(0.7692389013639721+0.6389612763136348j)'
assert repr(True ** 1j) == '(1+0j)'
assert str(1j ** 2) == '(-1+0j)'
assert str((1 + 1j) ** 2) == '2j'
assert repr(1j ** 1j) == '(0.20787957635076193+0j)'
assert repr((1 + 1j) ** 0.5) == '(1.0986841134678098+0.45508986056222733j)'
assert str(1j ** 0) == '(1+0j)'
assert str(0j ** 0) == '(1+0j)'

# negative-integer exponents divide one by the exact power, keeping signed zeros
assert repr(1j ** -2) == '(-1-0j)'
assert repr((1 + 1j) ** -3) == '(-0.25-0.25j)'
assert repr(1j ** -0.0) == '(1+0j)'

# zero base / zero denominator domain errors
try:
    0 ** 1j
except ZeroDivisionError as e:
    assert 'zero to a negative or complex power' in str(e)
else:
    raise AssertionError('expected ZeroDivisionError')
assert str(0 ** (2 + 0j)) == '0j'
try:
    0j ** -1
except ZeroDivisionError as e:
    assert 'zero to a negative or complex power' in str(e)
else:
    raise AssertionError('expected ZeroDivisionError')
try:
    (1e-200+0j) ** -2
except ZeroDivisionError:
    pass
else:
    raise AssertionError('expected ZeroDivisionError')

# ternary power rejects a complex operand (CPython ValueError)
def expect_value_error(fn):
    try:
        fn()
    except ValueError as e:
        assert 'complex modulo' in str(e), str(e)
    else:
        raise AssertionError('expected ValueError')


expect_value_error(lambda: pow(2, 1j, 5))
expect_value_error(lambda: pow(1j, 2, 5))

# overflow raises; underflow is silent
def expect_overflow(fn):
    try:
        fn()
    except OverflowError as e:
        assert 'complex exponentiation' in str(e)
    else:
        raise AssertionError('expected OverflowError')


expect_overflow(lambda: (1e308+0j) ** 2)
expect_overflow(lambda: (1e308+0j) ** 1.5)
expect_overflow(lambda: complex(float('inf'), 0) ** 2)
assert str((1e-308+0j) ** 2) == '0j'
assert repr(complex(float('inf'), 0) ** -2) == '0j'
assert repr(complex(float('nan'), 0) ** 2) == '(nan+nanj)'

# division by an infinite complex keeps CPython's signed zero
assert repr(1 / complex(float('inf'), 0)) == '-0j'
assert repr(1 / complex(float('inf'), 1)) == '-0j'

# infinite products recover infinities per C11 Annex G
assert repr(complex(float('inf'), float('nan')) * (1+0j)) == '(inf+nanj)'
assert repr(complex(float('nan'), float('inf')) * complex(float('inf'), 0)) == '(nan+infj)'

# in-place forms dispatch the reflected slots as well
x = 1
x += 1j
assert str(x) == '(1+1j)'

# floor-division/modulo/divmod and ordered comparisons keep their errors
def expect_type_error(fn, fragment):
    try:
        fn()
    except TypeError as e:
        assert fragment in str(e), str(e)
    else:
        raise AssertionError('expected TypeError')


expect_type_error(lambda: 5 % 1j, "unsupported operand type(s) for %: 'int' and 'complex'")
expect_type_error(lambda: 5 // 1j, "unsupported operand type(s) for //: 'int' and 'complex'")
expect_type_error(lambda: divmod(5, 1j), "unsupported operand type(s) for divmod(): 'int' and 'complex'")
expect_type_error(lambda: 1 < 1j, "'<' not supported between instances of 'int' and 'complex'")

# equality is order-independent and never raises
assert (1 == 1j, 1 != 1j, 1.0 == 1j, 1j == 1) == (False, True, False, False)

# single-operand complex paths unchanged
assert abs(3j) == 3.0
assert str(complex(3, -4)) == '(3-4j)'

# constants that differ only in a signed zero stay distinct in the bytecode
# constant pool (bit-pattern equality, not value equality)
q1 = 1j ** -2
r1 = 1j ** 2
assert repr(q1) == '(-1-0j)' and repr(r1) == '(-1+0j)'
q2 = 1j ** 2
r2 = 1j ** -2
assert repr(q2) == '(-1+0j)' and repr(r2) == '(-1-0j)'
print(repr(1j ** 2), repr(1j ** -2))

print("test_complex_reflected_arithmetic_regression passed")
