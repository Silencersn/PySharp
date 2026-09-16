# Regression test: complex(str) parses string arguments like CPython's
# complex_from_string_inner — real/imaginary forms, j-suffix forms, inf/nan
# literals, underscores between digits, an optional repr-style bracket, and
# ValueError for malformed input.
# NOTE: attribute checks (.real/.imag) are avoided — PySharp complex does
# not expose them yet (separate gap), so repr faces carry the assertions.

# core matrix from the issue
assert repr(complex('1+2j')) == '(1+2j)'
assert repr(complex('2j')) == '2j'
assert repr(complex('3')) == '(3+0j)'
assert repr(complex('inf')) == '(inf+0j)'
assert repr(complex('-Infinity')) == '(-inf+0j)'
assert repr(complex('nanj')) == 'nanj'

# malformed input raises ValueError with the dedicated message
for bad in ['', 'abc', '1+2', '1 + 2j', '(1+2j', '1+2j)', '.', '0x10', 'inj']:
    try:
        complex(bad)
    except ValueError as e:
        assert str(e) == 'complex() arg is a malformed string', (bad, e)
    else:
        raise AssertionError(bad)

# brackets, whitespace and legacy sign forms
assert repr(complex('  1+2j  ')) == '(1+2j)'
assert repr(complex('(1+2j)')) == '(1+2j)'
assert repr(complex('( 1+2j )')) == '(1+2j)'
assert repr(complex(' ( 1+2j ) ')) == '(1+2j)'
assert repr(complex('j')) == '1j'
assert repr(complex('J')) == '1j'
assert repr(complex('-j')) == '-1j'
assert repr(complex('+j')) == '1j'
assert repr(complex('1+j')) == '(1+1j)'
assert repr(complex('1-j')) == '(1-1j)'

# inf/nan literal faces in either component
assert repr(complex('infj')) == 'infj'
assert repr(complex('-Infinityj')) == '-infj'
assert repr(complex('nan+j')) == '(nan+1j)'
assert repr(complex('1e999')) == '(inf+0j)'
assert repr(complex('1e999j')) == 'infj'
assert repr(complex('-1e999+2j')) == '(-inf+2j)'
assert repr(complex('1e-999')) == '0j'

# exponent handling: the token is greedy, incomplete exponents fail
assert repr(complex('1e+0j')) == '1j'
assert repr(complex('+3.5e2-4.25E-1j')) == '(350-0.425j)'

# underscores are allowed only between digits; violations name the string
assert repr(complex('1_000.5')) == '(1000.5+0j)'
assert repr(complex('1_000.5j')) == '1000.5j'
assert repr(complex('1_0.5_2')) == '(10.52+0j)'
for bad in ['_1', '1_', '1__0', '1.5_j']:
    try:
        complex(bad)
    except ValueError as e:
        assert str(e) == f'could not convert string to complex: {bad!r}', (bad, e)
    else:
        raise AssertionError(bad)
try:
    complex("'1_'")
except ValueError as e:
    assert str(e) == 'could not convert string to complex: "\'1_\'"', e
else:
    raise AssertionError('quoted')

# strings are rejected in the two-argument form like CPython
for call in (lambda: complex('1', 2), lambda: complex(1, '2'),
             lambda: complex(real='1'), lambda: complex(1, imag='2')):
    try:
        call()
    except TypeError as e:
        assert 'must be a real number, not str' in str(e), e
    else:
        raise AssertionError('two-arg str')

print("test_complex_string_parsing_regression passed")
