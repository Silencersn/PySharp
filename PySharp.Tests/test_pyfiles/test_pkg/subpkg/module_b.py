# test_pkg.subpkg.module_b
# Test relative imports

# from . import (import from the same package: test_pkg.subpkg)
from . import module_c
assert module_c.module_c_var == "module_c_value"

# from .module import name
from .module_c import module_c_var as imported_c_var
assert imported_c_var == "module_c_value"

# from .. import (import from parent package: test_pkg)
from .. import module_a as parent_module_a
assert parent_module_a.module_a_var == "module_a_value"

# from .. import __init__ values
from .. import value_from_init
assert value_from_init == "init_value"
