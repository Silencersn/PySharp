"""member_descriptor and getset_descriptor are distinct Python types.

:kind: test
"""

# CPython builds two descriptor species from PyDescr_NewMember and
# PyDescr_NewGetSet (Objects/descrobject.c): each carries its own type
# name, its own repr sentence and its own failed-write message —
# PyMember_SetOne keeps the bare "readonly attribute", a setter-less
# getset names the attribute and its declaring type.

print(type(float.real).__name__)
print(type(int.numerator).__name__)
print(type(super.__dict__["__thisclass__"]).__name__)
print(repr(float.real))
print(repr(float.imag))
print(repr(int.denominator))
print(repr(super.__dict__["__thisclass__"]))


class S:
    pass


s = super(S, S())
try:
    s.__thisclass__ = S
except AttributeError as e:
    print("AttributeError:", e)
try:
    del s.__thisclass__
except AttributeError as e:
    print("AttributeError:", e)

try:
    (1.5).real = 2
except AttributeError as e:
    print("AttributeError:", e)

try:
    float.__mro__ = ()
except TypeError as e:
    print("TypeError:", e)
