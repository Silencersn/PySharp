"""
Regression: the complex constructor must bind 'real'/'imag' keyword
arguments (CPython complex_new_impl) — keyword forms used to be silently
ignored and always produced (0+0j). Keyword/positional conflicts, total
argument count, unexpected keywords, and component type errors all carry
CPython's messages; complex component values combine arithmetically as
cr + ci*1j (with a DeprecationWarning). Subclasses receive the constructed
value with their own type.

CPython 3.14.6 reference:
    complex(real=1, imag=2)  -> (1+2j)
    complex(real=3)          -> (3+0j)
    complex(imag=4)          -> 4j
    complex(1, imag=2)       -> (1+2j)
    complex(1, real=2)       -> TypeError: argument for complex() given by
                                name ('real') and position (1)
    complex(1, 2, imag=3)    -> TypeError: complex() takes at most 2
                                arguments (3 given)
    complex(foo=1)           -> TypeError: complex() got an unexpected
                                keyword argument 'foo'
    complex(real={})         -> TypeError: complex() argument 'real' must
                                be a real number, not dict
    complex(1, '2')          -> TypeError: complex() argument 'imag' must
                                be a real number, not str
    complex(3, 4j)           -> (-1+0j)
    complex(2j, 3j)          -> (-3+2j)
    complex(imag=2j)         -> (-2+0j)
"""

assert str(complex(real=1, imag=2)) == '(1+2j)'
assert str(complex(real=3)) == '(3+0j)'
assert str(complex(imag=4)) == '4j'
assert str(complex(imag=2.5)) == '2.5j'
assert str(complex(real=True, imag=2)) == '(1+2j)'
assert str(complex(1, imag=2)) == '(1+2j)'
assert str(complex()) == '0j'
assert str(complex(2j)) == '2j'
assert str(complex(2.5)) == '(2.5+0j)'
assert str(complex(5)) == '(5+0j)'


def expect_type_error(fn, fragment):
    try:
        fn()
    except TypeError as e:
        assert fragment in str(e), str(e)
    else:
        raise AssertionError('expected TypeError')


expect_type_error(lambda: complex(1, real=2), "given by name ('real') and position (1)")
expect_type_error(lambda: complex(1, 2, real=3), 'takes at most 2 arguments (3 given)')
expect_type_error(lambda: complex(1, 2, imag=3), 'takes at most 2 arguments (3 given)')
expect_type_error(lambda: complex(1, 2, 3), 'takes at most 2 arguments (3 given)')
expect_type_error(lambda: complex(1, 2, foo=1), 'takes at most 2 arguments (3 given)')
expect_type_error(lambda: complex(foo=1), "got an unexpected keyword argument 'foo'")
expect_type_error(lambda: complex(real={}), "'real' must be a real number, not dict")
expect_type_error(lambda: complex(real=[]), "'real' must be a real number, not list")
expect_type_error(lambda: complex(imag='2'), "'imag' must be a real number, not str")
expect_type_error(lambda: complex(1, '2'), "'imag' must be a real number, not str")

# complex component values combine as cr + ci*1j (CPython semantics)
assert str(complex(3, 4j)) == '(-1+0j)'
assert str(complex(2j, 0)) == '2j'
assert str(complex(2j, 3j)) == '(-3+2j)'
assert str(complex(real=2j, imag=1)) == '3j'
assert str(complex(imag=2j)) == '(-2+0j)'

# subclasses receive the constructed value with their own type
class C(complex):
    pass


c = C(real=1, imag=2)
assert type(c).__name__ == 'C'
assert str(c) == '(1+2j)'
s = C(2j)
assert type(s).__name__ == 'C'
assert str(s) == '2j'

print("test_complex_keyword_constructor_regression passed")
