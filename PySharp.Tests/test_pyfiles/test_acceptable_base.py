"""
sealed built-in types are not acceptable base types.

CPython's type_new computes best_base first and rejects any base without
Py_TPFLAGS_BASETYPE with "type 'X' is not an acceptable base type";
range and slice are static-layout types without that flag. The duplicate
base scan lives in the MRO step, after best_base, so a sealed base wins
over "duplicate base class" in combined-error cases. The check fires
inside type.__new__, so a custom metaclass sees its __new__ run before
the error propagates.

CPython 3.14 reference (Objects/typeobject.c best_base/check_duplicates,
type_new_impl).

:kind: test
"""

# --- the reported cases ---
try:
    class XR(range):
        pass
    raise AssertionError("range must not be subclassable")
except TypeError as e:
    assert str(e) == "type 'range' is not an acceptable base type", str(e)

try:
    class XS(slice):
        pass
    raise AssertionError("slice must not be subclassable")
except TypeError as e:
    assert str(e) == "type 'slice' is not an acceptable base type", str(e)

# --- dynamic type() creation hits the same check ---
try:
    type("XR2", (range,), {})
    raise AssertionError("range base must fail via type()")
except TypeError as e:
    assert str(e) == "type 'range' is not an acceptable base type", str(e)

try:
    type("XS2", (slice,), {})
    raise AssertionError("slice base must fail via type()")
except TypeError as e:
    assert str(e) == "type 'slice' is not an acceptable base type", str(e)

# --- controls: sealed bool still refused, heap int still allowed ---
try:
    class XB(bool):
        pass
    raise AssertionError("bool must not be subclassable")
except TypeError as e:
    assert str(e) == "type 'bool' is not an acceptable base type", str(e)

class XI(int):
    def double(self):
        return self * 2

assert XI(3).double() == 6 and isinstance(XI(3), int)

# --- error precedence: per-base sealed/layout checks run before the
# duplicate scan, matching best_base -> check_duplicates ordering ---
try:
    class P1(bool, range):
        pass
    raise AssertionError("first sealed base must be reported")
except TypeError as e:
    assert str(e) == "type 'bool' is not an acceptable base type", str(e)

try:
    class P2(range, bool):
        pass
    raise AssertionError("first base must be reported")
except TypeError as e:
    assert str(e) == "type 'range' is not an acceptable base type", str(e)

try:
    class P3(range, range):
        pass
    raise AssertionError("sealed base must beat duplicate detection")
except TypeError as e:
    assert str(e) == "type 'range' is not an acceptable base type", str(e)

try:
    class P4(int, int, range):
        pass
    raise AssertionError("sealed base must beat duplicate detection")
except TypeError as e:
    assert str(e) == "type 'range' is not an acceptable base type", str(e)

try:
    class P5(int, int):
        pass
    raise AssertionError("duplicate bases must still be refused")
except TypeError as e:
    assert str(e) == "duplicate base class int", str(e)

try:
    class P6(list, dict, list):
        pass
    raise AssertionError("layout conflict must still be refused")
except TypeError as e:
    assert str(e) == "multiple bases have instance lay-out conflict", str(e)

# --- a custom metaclass __new__ runs before type.__new__ refuses ---
_meta_log = []

class MetaAccept(type):
    def __new__(mcls, name, bases, ns):
        _meta_log.append(name)
        return super().__new__(mcls, name, bases, ns)

class MetaOk(object, metaclass=MetaAccept):
    pass

try:
    class MetaBad(range, metaclass=MetaAccept):
        pass
    raise AssertionError("range base must fail under a metaclass")
except TypeError as e:
    assert str(e) == "type 'range' is not an acceptable base type", str(e)

assert _meta_log == ["MetaOk", "MetaBad"], _meta_log

print("acceptable base passed")
