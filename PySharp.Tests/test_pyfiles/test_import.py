"""
Module importing and attribute access tests
"""

import test_imported

# Test basic import and access
assert hasattr(test_imported, 'foo')
assert test_imported.foo == 1
assert test_imported.bar == 2

# Test from ... import *
from test_imported import *
assert foo == 1
assert bar == 2

# Test from ... import ... as ...
from test_imported import foo as myfoo
assert myfoo == 1

print("test_import passed")
