"""
a from-import of a name missing from a package raises
ImportError ("cannot import name '<name>' from '<package>'"), never the
underlying AttributeError. The old IMPORT_FROM path let AttributeError
escape, so `except ImportError` could not handle optional dependencies.

CPython 3.14 reference (ceval.c import_from): an AttributeError raised
while getting the name is translated to ImportError with the module's
__name__; plain attribute access on the module is unaffected.

:kind: test
"""

# a present name imports normally
from test_from_import_pkg import exported

assert exported == 1

# a missing name raises ImportError catchable as such
try:
    from test_from_import_pkg import NotExported
    assert False, "ImportError expected"
except ImportError as e:
    assert "cannot import name 'NotExported' from 'test_from_import_pkg'" in str(e)

# the same holds for a second missing name
try:
    from test_from_import_pkg import also_missing
    assert False, "ImportError expected"
except ImportError as e:
    assert "cannot import name 'also_missing'" in str(e)

# plain attribute access on the module stays AttributeError
import test_from_import_pkg

try:
    test_from_import_pkg.NotExported
    assert False, "AttributeError expected"
except ImportError:
    assert False, "plain attribute access must not raise ImportError"
except AttributeError:
    pass

# the failure message trails the module's file location when it has one,
# "unknown location" otherwise (ceval's origin trail)
try:
    from test_from_import_pkg import NotExported2
    assert False, "ImportError expected"
except ImportError as e:
    assert "('unknown location')" not in str(e)
    assert str(e).startswith(
        "cannot import name 'NotExported2' from 'test_from_import_pkg' ("), str(e)
    assert str(e).rstrip().endswith("__init__.py)"), str(e)

try:
    from math import nosuchthing
    assert False, "ImportError expected"
except ImportError as e:
    assert str(e) == "cannot import name 'nosuchthing' from 'math' (unknown location)", str(e)

# an attribute miss falls back to the "<module>.<name>" entry of the import
# registry (sys.modules) before failing (ceval's relative-import fallback)
import test_from_import_pkg.sub
from test_from_import_pkg import sub
assert sub.exported == 1

# import of a missing module is still ModuleNotFoundError (an ImportError)
try:
    import no_such_module_nw150
    assert False, "ImportError expected"
except ImportError as e:
    assert isinstance(e, ModuleNotFoundError)

print("test_from_import_error passed")
