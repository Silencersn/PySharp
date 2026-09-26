"""
PEP 562 module-level __getattr__/__dir__ hooks.

A module-level __getattr__ resolves attributes the module namespace
misses (lazy exports, deprecation redirects); its exceptions propagate
unchanged, an AttributeError counting as "missing". The module type's
__dir__ method honors a module-dict __dir__ hook, lists the plain dict
keys without one, and dir() sorts the result.

CPython 3.14 reference (Objects/moduleobject.c module_getattro and
module_dir; PEP 562).

:kind: test
"""

import test_module_lazy_attrs_helper as helper

# hook resolves the attribute; present names bypass the hook
assert helper.magic == 42
assert helper.alpha == "letters"
assert helper.CONST == "const-value"

# the hook's own AttributeError surfaces verbatim
try:
    helper.nothere
    assert False, "AttributeError expected"
except AttributeError as e:
    assert str(e) == "module test_module_lazy_attrs_helper has no attribute 'nothere'"

# hasattr / getattr-with-default agree
assert hasattr(helper, "magic")
assert not hasattr(helper, "nothere")
assert getattr(helper, "magic", "dflt") == 42
assert getattr(helper, "nope", "dflt") == "dflt"

# alias import shares the module object
import test_module_lazy_attrs_helper as h2
assert h2 is helper

# from-import goes through the hook; a hook AttributeError is "missing"
from test_module_lazy_attrs_helper import magic
assert magic == 42
try:
    from test_module_lazy_attrs_helper import missing_name
    assert False, "ImportError expected"
except ImportError as e:
    assert "cannot import name 'missing_name'" in str(e)

# the hook receives the attribute name as a str
assert helper.namecheck == "str"

# non-AttributeError hook exceptions propagate unchanged
try:
    helper.boom
    assert False, "KeyError expected"
except KeyError as e:
    assert str(e) == "'boom-key'"

# dir(): module dict keys merged with the helper's advertised names,
# sorted; the hook itself is a listed global
d = dir(helper)
assert d == sorted(d)
assert "CONST" in d
assert "__getattr__" in d
assert "__dir__" in d
assert "magic" in d
assert "alpha" in d

# the helper's module-level __dir__ is callable directly
assert "CONST" in helper.__dir__()

# dir() honors the module-dict __dir__ hook, listifies and sorts
helper.__dir__ = lambda: ["zz", "aa"]
assert helper.__dir__() == ["zz", "aa"]
assert dir(helper) == ["aa", "zz"]
helper.__dir__ = lambda: (n for n in ["m2", "a1"])
assert dir(helper) == ["a1", "m2"]

# a non-callable __dir__ fails the same on both call paths
helper.__dir__ = 42
for attempt in (lambda: dir(helper), lambda: helper.__dir__()):
    try:
        attempt()
        assert False, "TypeError expected"
    except TypeError as e:
        assert "not callable" in str(e)

# a non-callable __getattr__ raises TypeError through the lookup
helper.__getattr__ = 42
try:
    helper.whatever
    assert False, "TypeError expected"
except TypeError as e:
    assert "not callable" in str(e)

# deleting the hook returns to the plain missing message
del helper.__getattr__
try:
    helper.magic
    assert False, "AttributeError expected"
except AttributeError as e:
    assert str(e) == "module 'test_module_lazy_attrs_helper' has no attribute 'magic'"

print("test_module_lazy_attrs passed")
