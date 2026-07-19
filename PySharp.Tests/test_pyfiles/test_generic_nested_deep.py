"""
Deep nested generic closure regression tests.
Uses only supported features: functions, nested classes, methods, tuples, and closures.
"""
print("testing generic nested deep")

def outer_func():
    x = 42

    class Outer[T]:
        outer_value = x

        def get_outer(self):
            return x, T

        class Middle:
            middle_value = x

            def get_middle(self):
                return x

            class Inner:
                inner_value = x

                def get_all(self):
                    def nested():
                        return x, T

                    return nested()

    return Outer

Outer = outer_func()
param = Outer.__type_params__[0]

assert Outer.outer_value == 42
assert Outer.get_outer(Outer()).__class__ is tuple
assert Outer.get_outer(Outer()) == (42, param)

assert Outer.Middle.middle_value == 42
assert Outer.Middle().get_middle() == 42

assert Outer.Middle.Inner.inner_value == 42
assert Outer.Middle.Inner().get_all() == (42, param)

print("test_generic_nested_deep passed")