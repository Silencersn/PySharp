"""
Tests for generic class capturing outer variables alongside type params.
"""
print("testing generic closure mix")

def outer_func():
    x = 42
    class Inner[T]:
        y = x
        z = T
    return Inner

Inner = outer_func()

assert Inner.y == 42, f"Expected 42, got {Inner.y}"
assert Inner.z.__name__ == "T"
assert Inner.z is Inner.__type_params__[0]

def outer_func_multi():
    x = 1
    y = 2

    class Inner[T, U, V]:
        values = (x, y, T, U, V)
        pair = (x, T)
        triple = (y, U, V)
        lookup = {x: T, y: U, T: V}

    return Inner

InnerMulti = outer_func_multi()
params = InnerMulti.__type_params__

assert len(params) == 3
assert params[0].__name__ == "T"
assert params[1].__name__ == "U"
assert params[2].__name__ == "V"
assert InnerMulti.values == (1, 2, params[0], params[1], params[2])
assert InnerMulti.pair == (1, params[0])
assert InnerMulti.triple == (2, params[1], params[2])
assert InnerMulti.lookup[1] is params[0]
assert InnerMulti.lookup[2] is params[1]
assert InnerMulti.lookup[params[0]] is params[2]

print("test_generic_closure_mix passed")