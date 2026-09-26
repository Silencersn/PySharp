"""Module with a module-level __getattr__ for the lazy attribute fixture.

:kind: helper
"""

_helpers = {"magic": 42, "alpha": "letters"}


def __getattr__(name):
    if name in _helpers:
        return _helpers[name]
    if name == "namecheck":
        return type(name).__name__
    if name == "boom":
        raise KeyError("boom-key")
    raise AttributeError("module test_module_lazy_attrs_helper has no attribute %r" % name)


def __dir__():
    return sorted(set(list(globals()) + list(_helpers)))


CONST = "const-value"
