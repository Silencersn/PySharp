"""Runtime dunder assignment and deletion on a class re-resolve the slot on the type and recursively on every subclass whose own dict does not shadow the name, and class creation resolves inherited slots from the MRO dicts for every slot family (CPython update_slot / fixup_slot_dispatchers).

:kind: test
"""

# Slot propagation: dunder assignment/deletion on a runtime class
# re-resolves the slot on the type and recursively on every subclass whose own
# dict does not shadow the name (CPython update_slot / update_subclasses), and
# class creation resolves inherited slots from the MRO dicts for every family,
# not only the object-defaultable nine (fixup_slot_dispatchers generalized).

# --- assignment reaches subclasses created earlier ---
calls = []

class A: pass
class B(A): pass

A.__add__ = lambda self, other: calls.append("A.add") or "sum"
assert A() + 1 == "sum"
assert B() + 1 == "sum"
assert calls == ["A.add", "A.add"]

class AIter: pass
class BIter(AIter): pass
AIter.__iter__ = lambda self: iter([1, 2])
assert list(BIter()) == [1, 2]

class ALen: pass
class BLen(ALen): pass
ALen.__len__ = lambda self: 7
assert len(BLen()) == 7

class ARepr: pass
class BRepr(ARepr): pass
ARepr.__repr__ = lambda self: "ARepr!"
assert repr(BRepr()) == "ARepr!"

# --- deletion re-resolves from the ancestors and propagates ---
del ALen.__len__
try:
    len(BLen())
    assert False, "len must fail after del __len__"
except TypeError:
    pass
ALen.__len__ = lambda self: 9
assert len(BLen()) == 9

class ADel: pass
class BDel(ADel): pass
ADel.__add__ = lambda self, other: "del-sum"
assert BDel() + 1 == "del-sum"
del ADel.__add__
try:
    BDel() + 1
    assert False, "__add__ must fail after del"
except TypeError:
    pass

# deleting an override re-inherits the ancestor's exact implementation
del ARepr.__repr__
assert repr(BRepr()).startswith("<")

# --- an intermediate base gaining the dunder later propagates down ---
class GainA:
    def __iter__(self):
        return iter(["a"])
class GainB(GainA): pass
class GainC(GainB): pass
assert list(GainC()) == ["a"]
GainB.__iter__ = lambda self: iter(["b"])
assert list(GainB()) == ["b"]
assert list(GainC()) == ["b"]

# --- a subclass's own entry shields its subtree; deeper levels follow it ---
class ShieldA: pass
class ShieldSub(ShieldA):
    def __len__(self):
        return 1
class ShieldSubSub(ShieldSub): pass
ShieldA.__len__ = lambda self: 100
assert len(ShieldSub()) == 1
assert len(ShieldSubSub()) == 1
ShieldSub.__len__ = lambda self: 2
assert len(ShieldSub()) == 2
assert len(ShieldSubSub()) == 2

# --- creation-time MRO-dict resolution covers non-defaultable families ---
# DupC's MRO is [DupC, DupB1, DupB2, DupP, object]: DupB1 merely inherits
# DupP's slot pointer (its own dict has no __iter__), so the first dict
# provider is DupB2 — the eager FillNullWith pass used to bake DupP's
# delegate in and mask DupB2's override
class DupP:
    def __iter__(self):
        return iter(["DupP"])
class DupB1(DupP): pass
class DupB2(DupP):
    def __iter__(self):
        return iter(["DupB2"])
class DupC(DupB1, DupB2): pass
assert list(DupC()) == ["DupB2"]

# --- hash family keeps its None / deletion semantics across subclasses ---
class HashA: pass
class HashB(HashA): pass
HashA.__hash__ = None
try:
    hash(HashB())
    assert False, "HashB must be unhashable while HashA.__hash__ is None"
except TypeError:
    pass
del HashA.__hash__
assert isinstance(hash(HashB()), int)
HashA.__hash__ = lambda self: 123456789
assert hash(HashB()) == 123456789

# --- __setattr__ propagates; deletion restores the hack-free default path ---
class SetA: pass
class SetB(SetA): pass
SetA.__setattr__ = lambda self, key, value: object.__setattr__(self, key, value * 2)
b = SetB()
b.k = 21
assert b.k == 42
del SetA.__setattr__
b.j = 3
assert b.j == 3

class Plain: pass
p = Plain()
p.x = 5
assert p.x == 5
del p.x
try:
    p.x
    assert False, "p.x must be gone"
except AttributeError:
    pass

# --- __init__/__new__ deletion falls back to object's delegate ---
# CPython update_one_slot resolves the deleted name to object's
# __init__/__new__ wrappers, so tp_init/tp_new re-wire to object's slot
# delegates: A(5) must raise (object's excess-argument check) and A() must
# succeed, the exact inverse of the override being alive
class InitA:
    def __init__(self, x):
        self.got = x
InitA(1)
del InitA.__init__
try:
    InitA(5)
    assert False, "InitA(5) must raise once __init__ is deleted"
except TypeError:
    pass
a = InitA()
assert not hasattr(a, "got")

class InitSub(InitA): pass
try:
    InitSub(5)
    assert False, "InitSub(5) must follow the deleted __init__"
except TypeError:
    pass
assert not hasattr(InitSub(), "got")

class NewA:
    created = []
    def __new__(cls):
        cls.created.append(cls)
        return super().__new__(cls)
NewA()
assert len(NewA.created) == 1
del NewA.__new__
NewA()
assert len(NewA.created) == 1, "deleted __new__ must stop running"

# --- assigning object's default __init__/__new__ back re-wires the slot ---
class ReInit:
    calls = []
    def __init__(self):
        ReInit.calls.append(self)
ReInit()
assert len(ReInit.calls) == 1
ReInit.__init__ = object.__init__
try:
    ReInit(1)
    assert False, "object.__init__ rejects excess arguments"
except TypeError:
    pass
ReInit()
assert len(ReInit.calls) == 1, "object.__init__ must have replaced the override"

class ReNew:
    made = []
    def __new__(cls):
        ReNew.made.append(cls)
        return super().__new__(cls)
ReNew()
assert len(ReNew.made) == 1
ReNew.__new__ = object.__new__
ReNew()
assert len(ReNew.made) == 1, "object.__new__ must have replaced the override"

# --- subclasses created BEFORE the deletion follow it too ---
class PreA:
    def __init__(self, x):
        self.got = x
class PreB(PreA): pass
PreB(1)
del PreA.__init__
try:
    PreB(5)
    assert False, "PreB(5) must follow the deleted inherited __init__"
except TypeError:
    pass
assert not hasattr(PreB(), "got")

class PreNewA:
    count = 0
    def __new__(cls):
        PreNewA.count += 1
        return super().__new__(cls)
class PreNewB(PreNewA): pass
PreNewB()
assert PreNewA.count == 1
del PreNewA.__new__
PreNewB()
assert PreNewA.count == 1, "existing subclass must follow the deleted __new__"

# --- class-body __init__/__new__ = object.* masks an overriding ancestor ---
# CPython's specific test wires object's tp_init/tp_new even at creation,
# so the explicit object-default assignment must beat the ancestor
class MaskInitA:
    ran = False
    def __init__(self):
        MaskInitA.ran = True
class MaskInitB(MaskInitA):
    __init__ = object.__init__
MaskInitB()
assert not MaskInitA.ran, "object.__init__ in the body must mask the ancestor"
try:
    MaskInitB(1)
    assert False, "object.__init__ rejects excess arguments"
except TypeError:
    pass

class MaskNewA:
    ran = False
    def __new__(cls):
        MaskNewA.ran = True
        return super().__new__(cls)
class MaskNewB(MaskNewA):
    __new__ = object.__new__
MaskNewB()
assert not MaskNewA.ran, "object.__new__ in the body must mask the ancestor"

# a later object-default assignment on the ANCESTOR also re-resolves down
MaskInitA.__init__ = object.__init__
MaskInitA.ran = False
class MaskInitC(MaskInitA): pass
MaskInitC()
assert not MaskInitA.ran

# --- native-base subclasses keep their inherited slots at creation ---
# pins the invariant behind creation-time ClearSlot: every non-null native
# slot has a same-name wrapper entry in its type's dict, so re-resolving a
# native subclass through the MRO dicts never wipes an inherited slot
class NativeInt(int): pass
assert NativeInt(3) + NativeInt(4) == 7
assert -NativeInt(2) == -2
assert NativeInt(5) == 5 and int(NativeInt(9)) == 9

class NativeStr(str): pass
assert NativeStr("ab") + NativeStr("cd") == "abcd"
assert len(NativeStr("abc")) == 3
assert NativeStr("xy").upper() == "XY"

class NativeList(list): pass
nl = NativeList([1, 2])
nl.append(3)
assert nl == [1, 2, 3]
assert len(nl) == 3
assert [x * 2 for x in nl] == [2, 4, 6]

class NativeDict(dict): pass
nd = NativeDict(a=1)
nd["b"] = 2
assert nd == {"a": 1, "b": 2}
assert sorted(nd.keys()) == ["a", "b"]

class NativeTuple(tuple): pass
nt = NativeTuple((1, 2))
assert nt + (3,) == (1, 2, 3)
assert len(nt) == 2
assert hash(nt) == hash((1, 2))

class NativeError(ValueError): pass
try:
    raise NativeError("boom")
except ValueError as e:
    assert str(e) == "boom"
else:
    assert False, "NativeError must be raisable and catchable as ValueError"

# --- type-level __delattr__ propagates and resolves back to object's ---
class DelA: pass
class DelB(DelA): pass
DelA.__delattr__ = lambda self, key: object.__delattr__(self, key)
db = DelB()
db.k = 1
del db.k
try:
    db.k
    assert False, "db.k must be gone"
except AttributeError:
    pass
DelA.__delattr__ = lambda self, key: object.__delattr__(self, key + "_gone")
db2 = DelB()
db2.k_gone = 5
del db2.k
try:
    db2.k_gone
    assert False, "the custom __delattr__ must have renamed the delete target"
except AttributeError:
    pass
del DelA.__delattr__
db4 = DelB()
db4.k = 1
del db4.k
assert not hasattr(db4, "k"), "deletion must fall back to the default path"
