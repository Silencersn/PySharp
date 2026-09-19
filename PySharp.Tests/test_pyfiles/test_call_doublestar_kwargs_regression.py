"""
Double-star call kwargs regression tests.

CPython compiles the kwargs of a **-call into one accumulating map, merging
consecutive explicit-keyword runs and each **mapping as separate groups in
source order. Pins, against CPython 3.14:
  - kwargs key order follows source appearance (mapping entries are not
    appended after explicit keywords)
  - evaluation order of keyword groups is source order
  - duplicate-key TypeError reports the first conflict in merge order
  - DICT_MERGE accepts mappings only: missing keys() -> "argument after **
    must be a mapping", single-arg KeyError from the lookup is re-reported
    as a duplicate keyword, other errors propagate unchanged
  - DICT_UPDATE (dict displays) keeps silent overwrite and surfaces
    "'X' object is not a mapping" for non-mappings
Message assertions use substrings so they stay valid whichever way the
exception-message rendering evolves.
"""


def collect(**kw):
    return list(kw)


# ===== 1. key order follows source appearance =====

def f(**kw):
    return kw

assert f(**{'a': 1}, b=2) == {'a': 1, 'b': 2}
assert f(b=2, **{'a': 1}) == {'b': 2, 'a': 1}
assert f(**{'m1': 1}, b=2, **{'m2': 2}, c=3) == {'m1': 1, 'b': 2, 'm2': 2, 'c': 3}
assert f(a=1, **{'m': 2}, b=3) == {'a': 1, 'm': 2, 'b': 3}
assert dict(**{'a': 1}, b=2) == {'a': 1, 'b': 2}
assert dict(b=2, **{'a': 1}) == {'b': 2, 'a': 1}

class C:
    def __init__(self, **kw):
        self.kw = kw

assert C(**{'a': 1}, b=2).kw == {'a': 1, 'b': 2}

class Q:
    def m(self, **kw):
        return list(kw)

assert Q().m(**{'q': 1}, r=2) == ['q', 'r']

# order observable through **kwargs forwarding
assert collect(**{'b': 1}, a=2) == ['b', 'a']
assert collect(a=2, **{'b': 1}) == ['a', 'b']
assert collect(**{'m1': 1}, b=2, **{'m2': 2}, c=3) == ['m1', 'b', 'm2', 'c']

# positional stars mixed with explicit keywords (no **mapping: single map)
assert (lambda *a, **kw: (a, kw))(*[1, 2], **{}, y=8) == ((1, 2), {'y': 8})
assert (lambda *a, **kw: (a, kw))(*[1, 2], y=8) == ((1, 2), {'y': 8})


# ===== 2. evaluation order of keyword groups is source order =====

def tag(value, log):
    log.append(value)
    return value

log = []
f(**tag({'a': 1}, log), b=tag(2, log))
assert log == [{'a': 1}, 2]

log = []
f(b=tag(2, log), **tag({'a': 1}, log))
assert log == [2, {'a': 1}]

log = []
f(**tag({'m1': 1}, log), b=tag(2, log), **tag({'m2': 2}, log), c=tag(3, log))
assert log == [{'m1': 1}, 2, {'m2': 2}, 3]


# ===== 3. duplicate-key TypeError reports the first conflict in merge order =====

def expect_multiple_values(fn, expected_key):
    try:
        fn()
    except TypeError as e:
        message = str(e)
        assert "got multiple values for keyword argument" in message, message
        assert "'" + expected_key + "'" in message, message
    else:
        raise AssertionError("expected TypeError")

expect_multiple_values(lambda: f(**{'a': 1, 'b': 1}, b=2, a=2), 'b')
expect_multiple_values(lambda: f(**{'x': 1}, x=2), 'x')
expect_multiple_values(lambda: f(x=2, **{'x': 1}), 'x')
expect_multiple_values(lambda: f(**{'k': 1}, **{'k': 2}), 'k')
expect_multiple_values(lambda: dict(**{'a': 1}, a=2), 'a')
expect_multiple_values(lambda: len(**{'object': 1}, object=2), 'object')


# ===== 4. DICT_MERGE accepts mappings only =====

class PlainMapping:
    def keys(self):
        return ['k']
    def __getitem__(self, key):
        return key * 10

assert f(**PlainMapping(), z=1) == {'k': 'kkkkkkkkkk', 'z': 1}

try:
    f(**1)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "argument after ** must be a mapping, not int" in str(e), str(e)

try:
    f(**"ab")
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "argument after ** must be a mapping, not str" in str(e), str(e)

# the _PyEval_FormatKwargsError lens covers the whole merge: an
# AttributeError or single-arg KeyError escaping __getitem__ is converted
# exactly like one from the keys() lookup
class GetItemKeyError:
    def keys(self):
        return ['k']
    def __getitem__(self, key):
        raise KeyError('boom')

try:
    f(**GetItemKeyError())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "got multiple values for keyword argument" in str(e), str(e)
    assert "'boom'" in str(e), str(e)

class GetItemAttrError:
    def keys(self):
        return ['k']
    def __getitem__(self, key):
        raise AttributeError('nope')

try:
    f(**GetItemAttrError())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "argument after ** must be a mapping" in str(e), str(e)

try:
    {**GetItemAttrError()}
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'GetItemAttrError' object is not a mapping" == str(e), str(e)

# the display path keeps KeyError raw (no duplicate-keyword conversion)
class GetItemKeyErrorOnly:
    def keys(self):
        return ['k']
    def __getitem__(self, key):
        raise KeyError('boom')

try:
    {**GetItemKeyErrorOnly()}
    raise AssertionError("expected KeyError")
except KeyError as e:
    assert "boom" in str(e), str(e)

# keys are streamed contains-checked before each __getitem__: a duplicate
# key in keys() raises instead of silently overwriting, and the colliding
# key never reaches __getitem__
getitem_log = []

class DupKeys:
    def keys(self):
        return ['x', 'x']
    def __getitem__(self, key):
        getitem_log.append(key)
        return 'v'

try:
    f(**DupKeys())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "got multiple values for keyword argument" in str(e), str(e)
    assert "'x'" in str(e), str(e)
assert getitem_log == ['x']

getitem_log.clear()
assert {**DupKeys()} == {'x': 'v'}
assert getitem_log == ['x', 'x']

# a conflict against an earlier keyword group is detected before any
# __getitem__ side effect
getitem_log.clear()

class ConflictMapping:
    def keys(self):
        return ['a', 'b']
    def __getitem__(self, key):
        getitem_log.append(key)
        return 'v'

try:
    f(a=1, **ConflictMapping())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "got multiple values for keyword argument" in str(e), str(e)
    assert "'a'" in str(e), str(e)
assert getitem_log == []

# keys() raising propagates through the lens: RuntimeError raw, lookup
# KeyError re-reported as a duplicate keyword
class KeysRaise:
    @property
    def keys(self):
        raise RuntimeError("boom keys")

try:
    f(**KeysRaise())
    raise AssertionError("expected RuntimeError")
except RuntimeError as e:
    assert "boom keys" in str(e), str(e)

class GetAttrKeyError:
    def __getattr__(self, name):
        raise KeyError("custom")

try:
    f(**GetAttrKeyError())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "got multiple values for keyword argument" in str(e), str(e)
    assert "'custom'" in str(e), str(e)

try:
    dict(**GetAttrKeyError())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "got multiple values for keyword argument" in str(e), str(e)

# keys() returning a non-iterable stays a TypeError on both paths (wording
# belongs to the known message family)
class BadKeys:
    def keys(self):
        return 5

try:
    f(**BadKeys())
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "iterable" in str(e), str(e)

try:
    {**BadKeys()}
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "iterable" in str(e), str(e)


# ===== 5. non-string keys keep the call-time keywords-must-be-strings check =====

try:
    f(**{1: 2})
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "keywords must be strings" in str(e), str(e)

assert {**{1: 2}} == {1: 2}


# ===== 6. dict-display DICT_UPDATE: silent overwrite, mapping-only wording =====

assert {**{'a': 1}, **{'a': 2}} == {'a': 2}
assert {**{'a': 1}, 'b': 2} == {'a': 1, 'b': 2}

try:
    {**1}
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'int' object is not a mapping" == str(e), str(e)

try:
    {**"ab"}
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'str' object is not a mapping" == str(e), str(e)

# a KeyError from the keys() lookup propagates raw (no duplicate-keyword
# conversion on the display path)
try:
    {**GetAttrKeyError()}
    raise AssertionError("expected KeyError")
except KeyError as e:
    assert "custom" in str(e), str(e)

# dict()/dict.update() keep the iterable-pairs fallback
assert dict([(1, 2)], a=3) == {1: 2, 'a': 3}
try:
    {}.update(1)
    raise AssertionError("expected TypeError")
except TypeError as e:
    assert "'int' object is not iterable" == str(e), str(e)
