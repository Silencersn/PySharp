"""complex() conversion order: __complex__ then __float__ then __index__.

:kind: test

CPython complex_new resolves the conversion protocol on the argument's type
MRO in this exact order; __complex__ runs for both the single-argument and
the real=/imag= keyword forms, and a non-complex return is a TypeError.
"""


def expect_raises(exc_type, fn):
    try:
        fn()
    except exc_type as exc:
        return exc
    raise AssertionError("expected " + exc_type.__name__)


class ComplexHook:
    def __complex__(self):
        return 3 + 4j


assert complex(ComplexHook()) == 3 + 4j
assert complex(real=ComplexHook(), imag=1) == 3 + 5j


class FloatHook:
    def __float__(self):
        return 2.5


assert complex(FloatHook()) == 2.5 + 0j
assert complex(real=FloatHook(), imag=1) == 2.5 + 1j


class IndexHook:
    def __index__(self):
        return 7


assert complex(IndexHook()) == 7 + 0j

# precedence: __complex__ wins over __float__ and __index__
class AllHooks:
    def __complex__(self):
        return 1j

    def __float__(self):
        return 99.0

    def __index__(self):
        return 99


assert complex(AllHooks()) == 1j


class ComplexThenFloat:
    def __complex__(self):
        raise AttributeError

    def __float__(self):
        return 1.5


# the lookup finds __complex__ but its failure propagates (no silent
# fallback past a found-but-failing method)
err = expect_raises(AttributeError, lambda: complex(ComplexThenFloat()))


class NonComplexReturn:
    def __complex__(self):
        return "bad"


err = expect_raises(TypeError, lambda: complex(NonComplexReturn()))
assert "__complex__ returned non-complex" in str(err), err

# no protocol at all keeps the historical message
err = expect_raises(TypeError, lambda: complex(object()))
assert "complex() argument must be a string or a number" in str(err), err

# the exact types still short-circuit without consulting the protocol
assert complex(2) == 2 + 0j
assert complex(2.5) == 2.5 + 0j
assert complex(True) == 1 + 0j

print("test_complex_conversion_protocol OK")
