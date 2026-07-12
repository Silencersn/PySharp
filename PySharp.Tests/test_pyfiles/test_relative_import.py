"""
Relative import tests

This script tests that explicit relative imports (PEP 328) work correctly.
It imports submodules that use relative imports internally.
"""

# First, import the package to ensure __init__ runs
import test_pkg

# Verify basic package import works
assert test_pkg.value_from_init == "init_value"

# Now import the subpackage module that uses relative imports
# This will trigger the relative import statements in module_b.py
import test_pkg.subpkg.module_b

# Verify module_b was successfully loaded (if its relative imports failed, it wouldn't load)
assert test_pkg.subpkg.module_b is not None

# Verify that the relative imports inside module_b succeeded
# module_b does: from . import module_c
assert test_pkg.subpkg.module_b.module_c is not None
assert test_pkg.subpkg.module_b.module_c.module_c_var == "module_c_value"
# module_b does: from .module_c import module_c_var as imported_c_var
assert test_pkg.subpkg.module_b.imported_c_var == "module_c_value"
# module_b does: from .. import module_a as parent_module_a
assert test_pkg.subpkg.module_b.parent_module_a is not None
assert test_pkg.subpkg.module_b.parent_module_a.module_a_var == "module_a_value"
# module_b does: from .. import value_from_init
assert test_pkg.subpkg.module_b.value_from_init == "init_value"

print("All relative import tests passed!")
