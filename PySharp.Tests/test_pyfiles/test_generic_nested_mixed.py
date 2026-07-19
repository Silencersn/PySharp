"""
Deep nested generic tests: class and function generics alternating.
Tests closure chains across multiple generic scopes.
"""
print("testing generic nested mixed")

# ===== Level 1: Generic function containing generic class =====
def outer_func[T]():
    class InnerClass[U]:
        t = T
        u = U
    return InnerClass

Cls = outer_func()
assert Cls.__type_params__[0].__name__ == "U"
assert Cls.t is outer_func.__type_params__[0]
assert Cls.u is Cls.__type_params__[0]

# ===== Level 2: Generic class containing generic function =====
class Outer[T]:
    def inner_func[K](self):
        return (T, K)

obj = Outer()
result = obj.inner_func()
assert result[0] is Outer.__type_params__[0]
assert result[1] is Outer.inner_func.__type_params__[0]
assert len(result) == 2

# ===== Level 3: Three levels — generic fn → generic class → generic method =====
def level1[A]():
    class level2[B]:
        a = A
        def level3[C](self):
            return (A, B, C)
    return level2

L2 = level1()
assert L2.a is level1.__type_params__[0]
assert L2.__type_params__[0].__name__ == "B"
l2_obj = L2()
result = l2_obj.level3()
assert len(result) == 3
assert result[0] is level1.__type_params__[0]  # A
assert result[1] is L2.__type_params__[0]       # B
assert result[2] is L2.level3.__type_params__[0] # C

# ===== Level 4: Generic class with generic method containing generic nested function =====
class Container[T]:
    value = T
    def method[U](self):
        def nested[V]():
            return (T, U, V)
        return nested

c = Container()
nested_fn = c.method()
assert nested_fn.__type_params__ is not None
assert len(nested_fn.__type_params__) == 1
assert nested_fn.__type_params__[0].__name__ == "V"
result = nested_fn()
assert len(result) == 3
assert result[0] is Container.__type_params__[0]        # T
assert result[1] is Container.method.__type_params__[0]  # U
assert result[2] is nested_fn.__type_params__[0]         # V

# ===== Level 5: Verify all __type_params__ are distinct across levels =====
assert Container.__type_params__[0] is not Container.method.__type_params__[0]
assert Container.__type_params__[0] is not nested_fn.__type_params__[0]
assert Container.method.__type_params__[0] is not nested_fn.__type_params__[0]

# ===== Level 6: All in one expression =====
class AllInOne[T]:
    def method[U](self):
        class Inner[V]:
            t = T
            u = U
            def inner_method[W](self):
                return (T, U, V, W)
        return Inner

ao = AllInOne()
InnerType = ao.method()
assert InnerType.t is AllInOne.__type_params__[0]
assert InnerType.u is AllInOne.method.__type_params__[0]
assert InnerType.__type_params__[0].__name__ == "V"
inner_obj = InnerType()
result = inner_obj.inner_method()
assert len(result) == 4
assert result[0] is AllInOne.__type_params__[0]            # T
assert result[1] is AllInOne.method.__type_params__[0]     # U
assert result[2] is InnerType.__type_params__[0]           # V
assert result[3] is InnerType.inner_method.__type_params__[0]  # W

print("test_generic_nested_mixed passed")
