"""Import target module exercised by the import statement fixtures.

:kind: helper
"""

__all__ = ['foo', 'bar']
foo = 1
bar = 2
baz = 3

def get_baz():
    return baz
