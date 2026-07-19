"""
Tests for generic function support (PEP 695) — __type_params__ on function objects.
"""
print("testing generic function")

# Test 1: Function generic __type_params__ basic
def single[T]():
    return T

assert single.__type_params__ is not None
params = single.__type_params__
assert len(params) == 1
assert params[0].__name__ == "T"
assert single() is single.__type_params__[0]

# Test 2: Multiple type params on function
def two_params[A, B]():
    return (A, B)

params = two_params.__type_params__
assert len(params) == 2
assert params[0].__name__ == "A"
assert params[1].__name__ == "B"
result = two_params()
assert result[0] is params[0]
assert result[1] is params[1]

# Test 3: TypeVar used in function body with calculation
def calc[X]():
    return (X, X)

assert calc.__type_params__[0].__name__ == "X"
res = calc()
assert len(res) == 2
assert res[0] is calc.__type_params__[0]
assert res[1] is res[0]

# Test 4: Function generic with class-level type param
class Holder[T]:
    def method[K](self):
        return (T, K)

assert Holder.method.__type_params__ is not None
assert len(Holder.method.__type_params__) == 1
assert Holder.method.__type_params__[0].__name__ == "K"

# Test 5: Identity — __type_params__ from different functions are distinct
def first[A]():
    return A
def second[B]():
    return B

assert first.__type_params__[0] is not second.__type_params__[0]
assert first() is first.__type_params__[0]
assert second() is second.__type_params__[0]

# Test 6: __type_params__ survives decorator (simple identity decorator)
def identity(f):
    return f

@identity
def decorated[T]():
    return T

assert decorated.__type_params__ is not None
assert len(decorated.__type_params__) == 1
assert decorated.__type_params__[0].__name__ == "T"

# Test 7: Three type params
def triple[A, B, C]():
    return (A, B, C)

params = triple.__type_params__
assert len(params) == 3
assert params[0].__name__ == "A"
assert params[1].__name__ == "B"
assert params[2].__name__ == "C"
result = triple()
assert result[0] is params[0]
assert result[1] is params[1]
assert result[2] is params[2]

# Test 8: __type_params__ is a regular tuple
assert type(single.__type_params__).__name__ == "tuple"

# Test 9: __doc__ on generic function
def doc_func[T]():
    """generic function doc"""
    return T

assert doc_func.__doc__ == "generic function doc"

# Test 10: Generic function with default argument
def default_arg[T](x=42):
    return (T, x)

result = default_arg()
assert len(result) == 2
assert result[0] is default_arg.__type_params__[0]
assert result[1] == 42, f"Expected 42, got {result[1]}"

# Test 11: Generic function with multiple default arguments
def multi_default[T](a=1, b="hello"):
    return (T, a, b)

result = multi_default()
assert len(result) == 3
assert result[0] is multi_default.__type_params__[0]
assert result[1] == 1
assert result[2] == "hello"

# Test 12: Decorator sees __type_params__ on function before applying
_deco_fn_log = []
def _capture_deco(f):
    _deco_fn_log.append(hasattr(f, "__type_params__"))
    _deco_fn_log.append(f.__type_params__[0].__name__ if hasattr(f, "__type_params__") else None)
    return f

@_capture_deco
def deco_func[A]():
    return A

assert deco_func.__type_params__[0].__name__ == "A"
assert _deco_fn_log[0] == True, f"decorator should see __type_params__, got {_deco_fn_log}"
assert _deco_fn_log[1] == "A"

# Test 13: Decorator that replaces the function
def _replace_deco(f):
    assert hasattr(f, "__type_params__")
    return "replaced"

@_replace_deco
def replaced_func[B]():
    return B

assert replaced_func == "replaced"

# Test 14: Chained decorators with generic function
_deco_chain_log = []
def _chain_one(f):
    _deco_chain_log.append("first")
    return f

def _chain_two(f):
    _deco_chain_log.append("second")
    return f

@_chain_one
@_chain_two
def chained_gen[C]():
    return C

assert _deco_chain_log == ["second", "first"], f"got {_deco_chain_log}"
assert chained_gen.__type_params__[0].__name__ == "C"

print("test_generic_function passed")
