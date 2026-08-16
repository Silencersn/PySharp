"""
Regression: int and float hashes must be consistent for equal values, so
dict/set mixed-key operations work.

CPython 3.14 reference:
    hash(1) == hash(1.0) == 1
    hash(-1) == hash(-1.0) == -2    (-1 is the error sentinel -> -2)
    hash(0.0) == hash(-0.0) == 0
    hash(2**62) == hash(2.0**62) == 2
    hash(float('inf')) == 314159
    {1:'a'}[1.0] == 'a', len({1, 1.0}) == 1, 1 in {1.0}
"""

# int/float hash consistency (hash invariant: equal values, equal hashes)
assert hash(1.0) == 1
assert hash(-1.0) == -2
assert hash(-1) == -2
assert hash(0.0) == 0
assert hash(-0.0) == 0
assert hash(True) == 1
assert hash(1.0) == hash(1)
assert hash(2.0 ** 62) == hash(2 ** 62)

# deterministic float hash values (CPython _Py_HashDouble)
assert hash(2.5) == 1152921504606846978
assert hash(3.14) == 322818021289917443
assert hash(float('inf')) == 314159
assert hash(float('-inf')) == -314159

# big int hash is reduced modulo 2**61 - 1 (bounded)
assert hash(10 ** 400) == 477640439047194790
assert hash(10 ** 400) < 2 ** 61

# dict/set with mixed int-float keys
assert {1: 'a'}[1.0] == 'a'
assert {1.0: 'b'}[1] == 'b'
assert len({1, 1.0}) == 1
assert 1 in {1.0}
assert 1.0 in {1}

print("test_hash_int_float_regression passed")
