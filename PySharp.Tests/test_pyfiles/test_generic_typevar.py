"""
Tests for TypeVar runtime objects in generic classes (PEP 695).
Comprehensive tests covering:
- __type_params__ on generic classes
- TypeVar identity, __name__, uniqueness across classes
- Multiple type params in expressions and tuples
- TypeVar in methods (regular, classmethod, staticmethod)
- TypeVar in nested classes via closure chain
- TypeVar inside comprehensions
- TypeVar in tuple/dict expressions
- TypeVar with base class inheritance
- Three or more type parameters
"""
print("testing generic typevar")

# ===== Section 1: Basic __type_params__ =====
class Box[T]:
    pass

assert len(Box.__type_params__) == 1
assert Box.__type_params__[0].__name__ == "T"

class Pair[T, U]:
    pass

params = Pair.__type_params__
assert len(params) == 2
assert params[0].__name__ == "T"
assert params[1].__name__ == "U"

# ===== Section 2: TypeVar in class body =====
class Box2[T]:
    x = T

assert Box2.x.__name__ == "T"
assert Box2.x is Box2.__type_params__[0]

# TypeVar in tuple expression
class Expr[T]:
    twice = (T, T)
    named = T

assert Expr.twice == (Expr.named, Expr.named)
assert Expr.twice[0] is Expr.__type_params__[0]
assert Expr.twice[0] is not Box2.__type_params__[0]
assert len(Expr.twice) == 2

# ===== Section 3: Multiple TypeVars in expressions =====
class Multi[T, U]:
    pair = (T, U)
    reverse = (U, T)

params = Multi.__type_params__
assert Multi.pair == (params[0], params[1])
assert Multi.reverse == (params[1], params[0])
assert Multi.pair[0] is params[0]
assert Multi.pair[1] is params[1]

# ===== Section 4: TypeVar in methods =====
class WithMethod[T]:
    value = T

    def get_type(self):
        return T

    @classmethod
    def class_get_type(cls):
        return T

    @staticmethod
    def static_get_type():
        return T

assert WithMethod.get_type(None).__name__ == "T"
assert WithMethod.get_type(None) is WithMethod.__type_params__[0]
assert WithMethod.class_get_type().__name__ == "T"
assert WithMethod.static_get_type().__name__ == "T"

# ===== Section 5: TypeVar in nested class (closure chain) =====
class Outer[T]:
    inner_T = T

    class Inner:
        def get_outer_T(self):
            return T

assert Outer.inner_T.__name__ == "T"
assert Outer.Inner().get_outer_T().__name__ == "T"
assert Outer.Inner().get_outer_T() is Outer.__type_params__[0]

# ===== Section 6: Identity — same TypeVar within class, different across classes =====
class IdCheck[T]:
    t1 = T
    t2 = T

assert IdCheck.t1 is IdCheck.t2
assert IdCheck.t1 is IdCheck.__type_params__[0]

class IdCheck2[T]:
    t1 = T

assert IdCheck.t1 is not IdCheck2.t1
assert IdCheck2.t1 is IdCheck2.__type_params__[0]

# ===== Section 7: TypeVar inside list comprehension =====
class Comp[T]:
    items = [T for _ in range(3)]

assert len(Comp.items) == 3
assert Comp.items[0] is Comp.__type_params__[0]
assert Comp.items[1] is Comp.items[0]
assert Comp.items[2] is Comp.items[0]

# ===== Section 8: TypeVar in dict expression =====
class Defaults[T]:
    default_tuple = (T, None)
    default_dict = {T: "typevar"}

assert Defaults.default_tuple[0] is Defaults.__type_params__[0]
assert Defaults.default_tuple[1] is None
assert Defaults.default_dict[Defaults.__type_params__[0]] == "typevar"

# ===== Section 9: TypeVar with base class inheritance =====
class Base:
    pass

class Derived[T](Base):
    t = T

assert issubclass(Derived, Base)
assert Derived.t is Derived.__type_params__[0]

# ===== Section 10: Three type params =====
class Triple[A, B, C]:
    all = (A, B, C)

params = Triple.__type_params__
assert len(params) == 3
assert params[0].__name__ == "A"
assert params[1].__name__ == "B"
assert params[2].__name__ == "C"
assert Triple.all == (params[0], params[1], params[2])

# ===== Section 11: __type_params__ is a regular tuple =====
assert type(Box.__type_params__).__name__ == "tuple"

# ===== Section 12: Multiple generic classes share the module =====
class Alpha[T]:
    t = T

class Beta[U]:
    u = U

assert Alpha.t is Alpha.__type_params__[0]
assert Beta.u is Beta.__type_params__[0]
assert Alpha.t is not Beta.u

# ===== Section 13: ParamSpec and TypeVarTuple names =====
class WithPs[T, *Ts, **P]:
    t = T
    ts = Ts
    p = P

assert WithPs.__type_params__[0].__name__ == "T"
assert WithPs.__type_params__[1].__name__ == "Ts"
assert WithPs.__type_params__[2].__name__ == "P"
assert WithPs.t.__name__ == "T"
assert WithPs.ts.__name__ == "Ts"
assert WithPs.p.__name__ == "P"

print("test_generic_typevar passed")
