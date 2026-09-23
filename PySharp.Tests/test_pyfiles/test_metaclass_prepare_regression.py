# Regression: the metaclass __prepare__ hook (PEP 3115) runs before the
# class body and its return value becomes the namespace the body executes
# in — the same object is then handed to the metaclass __new__/__init__.
# The hook resolves through a full attribute lookup on the metaclass
# (inherited type.__prepare__ included), class kwargs pass through to it,
# a non-mapping return raises TypeError, and a non-dict mapping still runs
# the body before type.__new__ rejects it. All pinned values are CPython
# 3.14 truth.

# --- the hook runs and its mapping reaches __new__/__init__ by identity ---
_made = {}
class MI(type):
    @classmethod
    def __prepare__(mcls, name, bases, **kw):
        _made[name] = {}
        return _made[name]

    def __new__(mcls, name, bases, ns, **kw):
        assert ns is _made[name]
        return super().__new__(mcls, name, bases, ns)

    def __init__(cls, name, bases, ns, **kw):
        assert ns is _made[name]
        super().__init__(name, bases, ns)

class PI(metaclass=MI):
    v = 3
assert PI.v == 3 and isinstance(_made["PI"], dict)

# --- class kwargs pass through to the hook and the metaclass call ---
_prep_kw = {}
class MK(type):
    @classmethod
    def __prepare__(mcls, name, bases, **kw):
        _prep_kw.update(kw)
        return {}

    def __new__(mcls, name, bases, ns, **kw):
        kw.clear()
        return super().__new__(mcls, name, bases, ns)

class PK(metaclass=MK, foo=1):
    pass
assert _prep_kw == {"foo": 1}

# --- an empty bases tuple reaches the hook untouched ---
_seen_bases = []
class MB4(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        _seen_bases.append(bases)
        return {}

class PB4(metaclass=MB4):
    pass
assert _seen_bases == [()]

# --- type.__prepare__ exists and hands out a plain dict ---
assert hasattr(type, "__prepare__")
assert type(type.__prepare__("X", ())) is dict

# a metaclass without its own hook inherits it and works
class MPlain(type):
    pass

class PPlain(metaclass=MPlain):
    pass
assert PPlain.__name__ == "PPlain"

# --- the most derived metaclass's hook wins ---
_order = []
class MA(type):
    @classmethod
    def __prepare__(mcls, name, bases, **kw):
        _order.append("MA")
        return {}

class MB(MA):
    @classmethod
    def __prepare__(mcls, name, bases, **kw):
        _order.append("MB")
        return {}

class XA(metaclass=MA):
    pass
class XB(metaclass=MB):
    pass
class XD(XA, XB):
    pass
assert _order == ["MA", "MB", "MB"]

# --- a hook raising aborts class creation ---
class MR(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        raise ValueError("boom")

Raised = "unchanged"
try:
    class Raised(metaclass=MR):
        pass
    assert False
except ValueError as e:
    assert str(e) == "boom"
assert Raised == "unchanged"

# --- a non-mapping return is rejected ---
class MNM(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        return 42

try:
    class PNM(metaclass=MNM):
        pass
    assert False
except TypeError as e:
    assert str(e) == "MNM.__prepare__() must return a mapping, not int"

# --- a plain-function hook is left unbound (missing mcls) ---
class MUF(type):
    def __prepare__(mcls, name, bases):
        return {}

try:
    class PUF(metaclass=MUF):
        pass
    assert False
except TypeError:
    pass

# --- class kwargs must satisfy the hook signature ---
class MSig(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        return {}

try:
    class PSig(metaclass=MSig, extra=5):
        pass
    assert False
except TypeError:
    pass

# --- a non-dict mapping: the body runs on it, type.__new__ rejects it ---
_nslog = []
class NMap:
    def __init__(self):
        self._d = {}
    def __getitem__(self, k):
        return self._d[k]
    def __setitem__(self, k, v):
        _nslog.append(k)
        self._d[k] = v
    def __delitem__(self, k):
        del self._d[k]
    def __iter__(self):
        return iter(self._d)
    def __len__(self):
        return len(self._d)

class MMap(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        return NMap()

try:
    class PMap(metaclass=MMap):
        a = 1
    assert False
except TypeError as e:
    assert str(e) == "type.__new__() argument 3 must be dict, not NMap"
assert "a" in _nslog and "__module__" in _nslog and "__qualname__" in _nslog

# --- a dict subclass namespace: overrides see every store, order kept ---
_dslog = []
class DSub(dict):
    def __setitem__(self, k, v):
        _dslog.append(k)
        dict.__setitem__(self, k, v)

class MD(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        return DSub()

    def __new__(mcls, name, bases, ns, **kw):
        globals()["_body_order"] = [k for k in ns if not k.startswith("__")]
        return super().__new__(mcls, name, bases, ns)

class PD(metaclass=MD):
    b = 1
    a = 2
assert PD.b == 1 and PD.a == 2
assert _body_order == ["b", "a"]
assert [k for k in _dslog if not k.startswith("__")] == ["b", "a"]

# --- a hook-returned dict is the live class namespace ---
class MD2(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        return {}

class PD2(metaclass=MD2):
    x = 1
    del x
assert not hasattr(PD2, "x")

class PD3(metaclass=MD2):
    x: int = 5
# the hook's namespace is the live class namespace, so the annotations read
# back from the class the hook built
assert "x" in PD3.__annotations__

# --- generic classes run the hook too ---
_gnames = []
class MG(type):
    @classmethod
    def __prepare__(mcls, name, bases, **kw):
        _gnames.append(name)
        return {}

class PG[T](metaclass=MG):
    pass
assert _gnames == ["PG"]

# --- the hook does not disturb zero-arg super in methods ---
class BS:
    def hi(self):
        return "BS"

class MSuper(type):
    @classmethod
    def __prepare__(mcls, name, bases):
        return {}

class PS(BS, metaclass=MSuper):
    def hi(self):
        return super().hi()
assert PS().hi() == "BS"

print("test_metaclass_prepare_regression OK")
