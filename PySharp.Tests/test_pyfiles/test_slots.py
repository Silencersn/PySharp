"""Verifies __slots__ semantics end to end: declared slots read/write/delete through member descriptors, undeclared names and __dict__ hit the dict-less errors, mangling, conflict and validation rules fire at class creation, and inheritance combines exactly like CPython.

:kind: test
"""

# test_slots: __slots__ declaration, storage and validation


def expect(exc_type, func, *args):
    try:
        func(*args)
        return "no exception"
    except exc_type as e:
        return str(e)


# --- core acceptance (issue 370) ------------------------------------------

class S:
    __slots__ = ("a",)

s = S()
s.a = 1
print("declared:", s.a)
print("setattr_undeclared:", expect(AttributeError, setattr, S(), "y", 1))
print("dictattr:", expect(AttributeError, getattr, S(), "__dict__"))
print("delattr_undeclared:", expect(AttributeError, delattr, S(), "y"))

class LC:
    pass

print("classassign:", expect(TypeError, setattr, S(), "__class__", LC))

class SL0:
    __slots__ = ()

print("empty_setattr:", expect(AttributeError, setattr, SL0(), "y", 1))
print("empty_dictattr:", expect(AttributeError, getattr, SL0(), "__dict__"))

# --- slot lifecycle --------------------------------------------------------

class SLX:
    __slots__ = ("x",)

print("unset_read:", expect(AttributeError, getattr, SLX(), "x"))
print("unset_delete:", expect(AttributeError, delattr, SLX(), "x"))
sx = SLX()
sx.x = 5
del sx.x
print("deleted_read:", expect(AttributeError, getattr, sx, "x"))
sx.x = 6
print("reassigned:", sx.x)

print("slots_attr:", SLX.__slots__)
print("descr_type:", type(SLX.__dict__["x"]).__name__)
print("descr_repr:", repr(SLX.__dict__["x"]))
print("dir_has_x:", "x" in dir(SLX()))
print("vars_has_x:", "x" in vars(SLX))
print("vars_has_dict:", "__dict__" in vars(SLX))

# --- mangling ---------------------------------------------------------------

class M:
    __slots__ = ("__priv",)

m = M()
m._M__priv = 1
print("mangled:", m._M__priv)
print("mangled_key:", "_M__priv" in vars(M))
print("mangled_read:", expect(AttributeError, getattr, m, "__priv"))

# --- validation -------------------------------------------------------------

try:
    exec("class Conflict:\n    x = 1\n    __slots__ = ('x',)")
    print("conflict: no error")
except ValueError as e:
    print("conflict:", e)

class Mix:
    y = 2
    __slots__ = ("z",)

mm = Mix()
mm.z = 3
print("mix:", Mix.y, mm.z)

# --- inheritance ------------------------------------------------------------

class PlainBase:
    pass

class SlotsOnPlain(PlainBase):
    __slots__ = ("x",)

sp = SlotsOnPlain()
sp.x = 1
sp.w = 2
print("slots_on_plain:", sp.x, sp.w, sp.__dict__)

class SlotsBase:
    __slots__ = ("a",)

class SlotsDerived(SlotsBase):
    __slots__ = ("b",)

sd = SlotsDerived()
sd.a = 1
sd.b = 2
print("derived_dict:", expect(AttributeError, getattr, sd, "__dict__"))
print("derived_setattr:", expect(AttributeError, setattr, sd, "c", 3))
print("derived_ok:", sd.a, sd.b)

# a secondary base with a dict hands it back
class OtherPlain:
    pass

class SlotsTwoWay(SlotsBase, OtherPlain):
    __slots__ = ("b",)

stw = SlotsTwoWay()
stw.a = 1
stw.extra = 2
print("two_way:", stw.a, stw.extra, stw.__dict__)

class SlotsDict:
    __slots__ = ("__dict__",)

sdd = SlotsDict()
sdd.anything = 9
print("claimed_dict:", sdd.anything, sdd.__dict__)

try:
    exec("class BadDict(PlainBase):\n    __slots__ = ('__dict__',)")
    print("bad_dict: no error")
except TypeError as e:
    print("bad_dict:", e)

try:
    exec("class BadTuple(tuple):\n    __slots__ = ('x',)")
    print("bad_tuple: no error")
except TypeError as e:
    print("bad_tuple:", e)

# __weakref__ declared is honored as "no weakref slot" and stays silent
class SlotsWeakref:
    __slots__ = ("__weakref__",)

print("weakref_setattr:", expect(AttributeError, setattr, SlotsWeakref(), "y", 1))
print("weakref_vars_dict:", "__dict__" in vars(SlotsWeakref))

print("nonstr_item:", expect(TypeError, exec, "class A:\n    __slots__ = (1,)"))
print("not_identifier:", expect(TypeError, exec, "class B:\n    __slots__ = ('a b',)"))
print("not_iterable:", expect(TypeError, exec, "class C:\n    __slots__ = 5"))

class Single:
    __slots__ = "q"

ss = Single()
ss.q = 1
print("single_str:", ss.q)

# --- type() 3-arg form -------------------------------------------------------

TSlot = type("TSlot", (), {"__slots__": ("k",)})
t = TSlot()
t.k = 4
print("type3_setattr:", expect(AttributeError, setattr, t, "j", 1))
print("type3:", t.k, "k" in vars(TSlot))

# layout algebra: two dict-less classes may swap __class__, a plain class may not
class SA:
    __slots__ = ()

class SB:
    __slots__ = ()

obj = SA()
obj.__class__ = SB
print("same_layout_swap:", type(obj).__name__)

print("All slots tests passed")
