"""
complex hash must agree with int/float hashes for equal
values. hash(complex(1, 0)) returned a HashCode-based value disjoint
from hash(1), so cross-type dict/set lookups silently missed.

CPython 3.14 reference (complex_hash):
    hash(complex(1, 0)) == hash(1.0) == hash(1)
    {1: 'a'}[complex(1, 0)] == 'a', {complex(1, 0): 'b'}[1] == 'b'
    len({1, 1.0, complex(1, 0), 0j}) == 2

:kind: test
"""

assert hash(complex(1, 0)) == hash(1)
assert hash(0j) == hash(0)
assert hash(complex(1, 0)) == hash(1.0) == hash(1)

# Zero (or -0.0) imaginary part keeps the real part's numeric hash
assert hash(complex(-1, -0.0)) == hash(-1) == -2
assert hash(complex(-1.0, 0)) == hash(-1.0)

# Large values reduce consistently through the double hash
assert hash(complex(2.0 ** 61, 0)) == hash(2 ** 61)

# Non-zero imaginary part uses the combined formula, stable per value
assert hash(complex(3.5, 1.5)) == hash(complex(3.5, 1.5))
assert hash(complex(float('inf'), 2)) == 2314165

# Cross-type dict lookups in both directions
d1 = {1: 'int'}
assert d1.get(complex(1, 0)) == 'int'
assert d1[complex(1, 0)] == 'int'
d2 = {complex(1, 0): 'cx'}
assert d2.get(1) == 'cx'
assert d2[1.0] == 'cx'

# set dedup across numeric types
assert len({1, complex(1, 0)}) == 1
assert len({1, 1.0, complex(1, 0), 0j}) == 2

print("test_complex_hash passed")
