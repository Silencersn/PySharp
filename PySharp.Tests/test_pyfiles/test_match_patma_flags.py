"""
match statement sequence/mapping dispatch must be a pure
type-flag check (CPython Py_TPFLAGS_SEQUENCE / Py_TPFLAGS_MAPPING),
independent of slot presence.

CPython 3.14 references:
- MATCH_SEQUENCE / MATCH_MAPPING are `tp_flags & Py_TPFLAGS_*` bitwise
  tests (Python/bytecodes.c); no slot probe, no dict special case.
- str/bytes/bytearray have sq_item yet carry no SEQUENCE flag, so they
  must not match sequence patterns.
- a user class defining __getitem__ gets no flag either — flag bits are
  set only by static definition and inherited via inherit_patma_flags
  (Objects/typeobject.c:8713).
- _PyEval_MatchKeys fetches values through the subject's two-argument
  get(key, sentinel), so __missing__ side effects never fire; a dotted
  value key colliding with a literal key raises ValueError at runtime
  (ceval.c:768-773; literal-only duplicates are the compiler's
  SyntaxError).
- _PyEval_MatchClass honors _Py_TPFLAGS_MATCH_SELF only when the class
  does not define __match_args__ (ceval.c:874-891); complex carries it.

:kind: test

:cpython-diff: CPython 3.14 rejects complex(...) match values while PySharp accepts them; divergence pending fix, remove this exemption when resolved
"""


class MyList(list):
    pass


class MyDict(dict):
    pass


class GetItemOnly:
    def __getitem__(self, index):
        if index == 0:
            return "a"
        if index == 1:
            return "b"
        raise IndexError(index)


class MissingDict(dict):
    def __missing__(self, key):
        # must never run during a mapping pattern (get(key, sentinel))
        self.missed = True
        return "missing"


# --- sequence patterns ---

match MyList([1, 2]):
    case [a, b]:
        assert a == 1 and b == 2
    case _:
        assert False, "list subclass must match sequence pattern"

match [1, 2]:
    case [a, b]:
        assert a == 1 and b == 2
    case _:
        assert False, "list must match sequence pattern"

match (1, 2):
    case [a, b]:
        assert a == 1 and b == 2
    case _:
        assert False, "tuple must match sequence pattern"

match range(2):
    case [a, b]:
        assert a == 0 and b == 1
    case _:
        assert False, "range must match sequence pattern"

# str/bytes/bytearray: sq_item exists, flag does not -> no match
for subject in ("ab", b"ab", bytearray(b"ab")):
    matched = False
    match subject:
        case [a, b]:
            matched = True
        case _:
            pass
    assert not matched, f"{type(subject).__name__} must not match sequence pattern"

# a user class with __getitem__ but no flag: not a sequence pattern target
matched = False
match GetItemOnly():
    case [a, b]:
        matched = True
    case _:
        pass
assert not matched, "user __getitem__ class must not match sequence pattern"

# --- mapping patterns ---

match MyDict({"k": 1}):
    case {"k": v}:
        assert v == 1
    case _:
        assert False, "dict subclass must match mapping pattern"

match {"k": 1}:
    case {"k": v}:
        assert v == 1
    case _:
        assert False, "dict must match mapping pattern"

# __missing__ must not fire: values come from get(key, sentinel)
md = MissingDict()
md["k"] = 1
match md:
    case {"k": v}:
        assert v == 1
    case _:
        assert False, "dict subclass must match mapping pattern"
assert not hasattr(md, "missed"), "__missing__ must not run during mapping pattern"

# --- class patterns: MATCH_SELF via flags ---

match complex(1, 2):
    case complex(x):
        assert x == complex(1, 2)
    case _:
        assert False, "complex must accept one positional sub-pattern"

match 3:
    case int(x):
        assert x == 3
    case _:
        assert False, "int must accept one positional sub-pattern"

match "s":
    case str(x):
        assert x == "s"
    case _:
        assert False, "str must accept one positional sub-pattern"

# MATCH_SELF only applies without __match_args__: a user class defining
# __match_args__ uses it even though int's MATCH_SELF is inherited
# (int.real is not modeled here, so the attribute is a plain class attr)
class MatchArgsInt(int):
    __match_args__ = ("tag",)
    tag = "seven"


class BareInt(int):
    pass


match MatchArgsInt(7):
    case MatchArgsInt(v):
        assert v == "seven", "__match_args__ must win over inherited MATCH_SELF"
    case _:
        assert False, "should match"

match BareInt(7):
    case BareInt(v):
        assert v == 7, "inherited MATCH_SELF allows one positional sub-pattern"
    case _:
        assert False, "should match"

# no __match_args__ and no flag: zero positional sub-patterns allowed
class Plain:
    pass


try:
    match Plain():
        case Plain(x):
            assert False, "must not match"
except TypeError as e:
    assert "accepts 0 positional sub-patterns" in str(e)
else:
    assert False, "TypeError expected"

# --- duplicate mapping key ---
# literal duplicate keys are a compile-time SyntaxError (both CPython's
# compiler and PySharp's SemanticAnalyzer reject them); a dotted value
# key colliding with a literal key reaches the runtime ValueError of
# _PyEval_MatchKeys (ceval.c:768-773)
class KeyBox:
    a = "a"


try:
    match {"a": 1}:
        case {"a": x, KeyBox.a: y}:
            assert False, "must not match"
except ValueError as e:
    assert "duplicate key" in str(e)
else:
    assert False, "ValueError expected"

print("ok")
