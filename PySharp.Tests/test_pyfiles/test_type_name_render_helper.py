# Helper for test_type_name_render_regression.py: a class created at runtime
# whose __qualname__ differs from its __name__, both at module level and inside
# a function. Imported by the regression fixture so the module-qualified forms
# (%T) can be checked from outside __main__.

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
