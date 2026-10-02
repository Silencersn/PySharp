"""
Tests for typing.ParamSpec and typing.TypeVarTuple (issue #511).
Tests:
- Manual construction: repr variance marker, args/kwargs views
- ParamSpecArgs/ParamSpecKwargs identity and __origin__
- PEP 695 syntax creates the right runtime objects (**P, *Ts, T)
- __type_params__ round-trip and attribute shapes

:kind: test
"""
import typing

print(typing.ParamSpec, typing.TypeVarTuple, typing.ParamSpecArgs, typing.ParamSpecKwargs)

P = typing.ParamSpec("P")
Ts = typing.TypeVarTuple("Ts")
print(repr(P), repr(Ts))
print(P.__name__, Ts.__name__)
print(P.__covariant__, P.__contravariant__, P.__infer_variance__)
print(P.args, P.kwargs)
print(P.args.__origin__ is P, P.kwargs.__origin__ is P)
print(type(P.args).__name__, type(P.kwargs).__name__)
print(repr(typing.ParamSpecArgs(P)), repr(typing.ParamSpecKwargs(P)))

class D[**P2]: pass
P2, = D.__type_params__
print(repr(P2), type(P2).__name__, P2.__infer_variance__)

class E[*Ts2]: pass
Ts2, = E.__type_params__
print(repr(Ts2), type(Ts2).__name__)

class F[T]: pass
T, = F.__type_params__
print(repr(T), type(T).__name__)
print(P != Ts)

try:
    typing.ParamSpec()
except TypeError as e:
    print("no name:", type(e).__name__)
try:
    typing.TypeVarTuple(1)
except TypeError as e:
    print("bad name:", type(e).__name__)
