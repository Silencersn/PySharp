# Regression: str subclass instances keep their subclass type —
# construction retags a fresh copy (unicode_subtype_new) instead of
# returning a shared exact str, so subclass methods resolve, overrides
# win, and instance dicts work; bytes/float/tuple construction copies
# for subtypes too instead of retagging shared instances process-wide.
# All pinned semantics are CPython 3.14 ground truth.

# --- the issue's faces ---
class MyStr(str):
    def ping(self):
        return "pong"

ms = MyStr("hi")
assert type(ms) is MyStr
assert isinstance(ms, str)
assert ms.ping() == "pong"
assert MyStr("x").ping() == "pong"

class Loud(str):
    def upper(self):
        return "LOUD[" + str.__repr__(self) + "]"
    def shout(self):
        return self.upper() + "!"

lv = Loud("hey")
assert lv.upper() == "LOUD['hey']"
assert lv.shout() == "LOUD['hey']!"

# --- construction produces the subclass, never a shared exact str ---
class MS(str):
    pass

empty = ""
value = "hi"
assert type(MS("hi")) is MS
assert type(MS()) is MS
assert MS() is not empty
assert MS(value) is not value
assert type(str.__new__(MS, "x")) is MS
assert type(str.__new__(MS)) is MS
assert type(MS(MS("x"))) is MS

class SubStr(MS):
    pass

assert type(SubStr("x")) is SubStr
assert SubStr("x").upper() == "X"

# str() over a subclass instance converts back to the exact type
assert type(str(ms)) is str
assert str(ms) == "hi"
assert type(f"{ms}") is str
assert type("{}".format(ms)) is str
assert type(ms + "!") is str
assert type(ms.upper()) is str
assert repr(ms) == "'hi'"

# encoding path keeps the subtype too
assert type(MS(b"hi", "utf-8")) is MS
assert MS(b"hi", "utf-8") == "hi"
assert type(MS(b"hi", encoding="utf-8", errors="strict")) is MS
assert MS(b"hi", encoding="utf-8", errors="strict") == "hi"

# --- value semantics stay str-compatible ---
assert MS("hi") == "hi" and "hi" == MS("hi")
assert hash(MS("hi")) == hash("hi")
assert {"hi": 1}[MS("hi")] == 1
assert MS("hi") in {"hi": 2}
assert ms * 2 == "hihi"

# --- subclass instances carry an instance dict ---
ms.foo = 1
assert ms.foo == 1

# --- sibling builtins keep subclass visibility (already worked) ---
class MB(bytes):
    def ping(self):
        return "b"
class MT(tuple):
    def ping(self):
        return "t"
class MF(float):
    def ping(self):
        return "f"

assert MB(b"x").ping() == "b"
assert MT((1,)).ping() == "t"
assert MF(1.5).ping() == "f"
assert type(MB()) is MB and type(MT()) is MT and type(MF()) is MF

# --- construction must not retype shared instances process-wide ---
b = b"ab"
MB(b)
assert type(b) is bytes
f = 1.5
MF(f)
assert type(f) is float
s = "hi"
MS(s)
assert type(s) is str
MB()
MT()
assert type(bytes()) is bytes
assert type(tuple()) is tuple
assert type(b"") is bytes
lit_tuple = ()
assert type(lit_tuple) is tuple

# exact-type construction keeps identity passthrough
b2 = b"x"
assert bytes(b2) is b2
t2 = (1,)
assert tuple(t2) is t2
f2 = 2.5
assert float(f2) is f2
s2 = "y"
assert str(s2) is s2

# subtype copies stay value-equal
assert MB(b"ab") == b"ab"
assert MT((1, 2)) == (1, 2)
assert MF(2.5) == 2.5
assert MS("hi") == "hi"

print("test_str_subclass_regression passed")
