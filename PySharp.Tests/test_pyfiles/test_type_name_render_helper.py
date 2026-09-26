"""Class factory imported by the qualified type-name rendering fixture.

Defines runtime classes whose __qualname__ differs from __name__, at module
level and inside a function, so the module-qualified forms can be checked
from outside __main__.

:kind: helper
"""

class ModuleClass:
    __hash__ = None


class ModuleProp:
    @property
    def p(self):
        return 1


class ModuleErr(Exception):
    pass


def make():
    class LocalClass:
        __hash__ = None

    return LocalClass
