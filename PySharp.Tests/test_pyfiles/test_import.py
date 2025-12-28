import test_imported
assert hasattr(test_imported, 'foo')
assert hasattr(test_imported, 'bar')
assert hasattr(test_imported, 'baz')
assert test_imported.foo == 1
assert test_imported.bar == 2
assert test_imported.get_baz() == 3

from test_imported import *
assert foo == 1
assert bar == 2

try:
    baz
    assert False
except NameError:
    pass

from test_imported import foo as myfoo, bar
assert myfoo == 1
assert bar == 2
